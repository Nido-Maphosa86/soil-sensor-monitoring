// What this file is for: everything about alerts: the alerts list and counters, acknowledge and resolve with a note, the bell panel, and the evaluator that raises new alerts.
// How it fits in: Service folder. The Alerts controller, the bell and the background worker call these through interfaces.
// Acknowledge and Resolve write the action AND the new state in ONE database update (see IAlertRepository.SaveActionAsync).
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly DataScope _scope;
    private readonly ICsvWriter _csv;

    public AlertService(IAlertRepository alerts, IUserRepository users, IClock clock, DataScope scope, ICsvWriter csv)
    {
        _alerts = alerts;
        _users = users;
        _clock = clock;
        _scope = scope;
        _csv = csv;
    }

    public async Task<AlertsPageDto> GetPageAsync(UserContext ctx, string? chip, string? search, string? siteId)
    {
        var now = _clock.UtcNow;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var mine = (await _alerts.GetAllAsync(sites.Select(s => s.SiteId))).ToList();
        var names = await UserNamesAsync();

        var open = mine.Count(a => a.Status == AlertStatus.Open);
        var ack = mine.Count(a => a.Status == AlertStatus.Acknowledged);
        var resolvedRecent = mine.Where(a => a.Status == AlertStatus.Resolved && a.Resolved is { } r && r.ActedAt >= now.AddDays(-30)).ToList();
        var avg = resolvedRecent.Count == 0 ? "No alerts closed yet"
            : $"Average time to close {resolvedRecent.Average(a => (a.Resolved!.ActedAt - a.RaisedAt).TotalDays):F1} days";

        IEnumerable<Alert> filtered = mine;
        if (!string.IsNullOrWhiteSpace(siteId)) filtered = ctx.CanSee(siteId) ? filtered.Where(a => a.SiteId == siteId) : filtered.Where(_ => false);
        filtered = (chip ?? "All") switch
        {
            "Critical" => filtered.Where(a => a.AlertType != AlertType.Offline && a.Severity == AlertSeverity.Critical),
            "Warning" => filtered.Where(a => a.AlertType != AlertType.Offline && a.Severity == AlertSeverity.Warning),
            "Offline" => filtered.Where(a => a.AlertType == AlertType.Offline),
            "Acknowledged" => filtered.Where(a => a.Status == AlertStatus.Acknowledged),
            "Resolved" => filtered.Where(a => a.Status == AlertStatus.Resolved),
            _ => filtered
        };

        var rows = filtered.OrderByDescending(a => a.RaisedAt).Select(a => ToRow(a, siteNames, names, now)).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            rows = rows.Where(r => new[] { r.Sensor, r.Province, r.Parameter, r.KindLabel, r.Owner }.Any(v => v.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        return new AlertsPageDto($"{open} open · {ack} acknowledged · {resolvedRecent.Count} resolved this month", open, ack, resolvedRecent.Count, avg, rows,
            sites.Select(s => (s.SiteId, s.Name)).ToList());
    }

    public async Task AcknowledgeAsync(UserContext ctx, string alertId, string? note)
    {
        var alert = await RequireVisibleAsync(ctx, alertId);
        AlertWorkflow.Acknowledge(alert, note, ctx.UserId, _clock.UtcNow); // refuses an empty note and a wrong state
        await _alerts.SaveActionAsync(alert, resolved: false);
    }

    public async Task ResolveAsync(UserContext ctx, string alertId, string? note)
    {
        var alert = await RequireVisibleAsync(ctx, alertId);
        AlertWorkflow.Resolve(alert, note, ctx.UserId, _clock.UtcNow);
        await _alerts.SaveActionAsync(alert, resolved: true);
    }

    public async Task<AlertNoteDto> GetNoteAsync(UserContext ctx, string alertId)
    {
        var alert = await RequireVisibleAsync(ctx, alertId);
        var action = alert.Resolved ?? alert.Acknowledged;
        var owner = action is null ? "—" : (await UserNamesAsync()).GetValueOrDefault(action.UserId, "—");
        var when = action is null ? "" : Display.Local(action.ActedAt).ToString("d MMMM yyyy HH:mm", Display.Culture);
        return new AlertNoteDto(alert.AlertId, alert.SensorId, alert.Status.ToString(), action?.Note ?? "No note was saved.", owner, when);
    }

    public async Task<byte[]> ExportCsvAsync(UserContext ctx)
    {
        var now = _clock.UtcNow;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var names = await UserNamesAsync();
        var rows = (await _alerts.GetAllAsync(sites.Select(s => s.SiteId))).OrderByDescending(a => a.RaisedAt).Select(a =>
        {
            var r = ToRow(a, siteNames, names, now);
            return (IEnumerable<string>)new[] { Display.Local(a.RaisedAt).ToString("yyyy-MM-dd HH:mm"), r.KindLabel, r.Province, r.Sensor, r.Parameter, r.Reading, r.Threshold, r.Owner, a.Status.ToString(), (a.Resolved ?? a.Acknowledged)?.Note ?? "" };
        });
        return _csv.Write(new[] { "Raised", "Type", "Province", "Sensor", "Parameter", "Reading", "Threshold", "Owner", "Status", "Note" }, rows);
    }

    private async Task<Alert> RequireVisibleAsync(UserContext ctx, string alertId)
    {
        // The alert lives under its location, so look through the locations this person may see (never others).
        foreach (var site in await _scope.VisibleSitesAsync(ctx))
            if (await _alerts.GetAsync(site.SiteId, alertId) is { } alert) return alert;
        throw new NotFoundException("That alert was not found.");
    }

    private async Task<Dictionary<string, string>> UserNamesAsync() => (await _users.GetAllAsync()).ToDictionary(u => u.UserId, u => u.Name);

    private static AlertRowDto ToRow(Alert a, Dictionary<string, string> siteNames, Dictionary<string, string> userNames, DateTime now)
    {
        var kind = a.AlertType == AlertType.Offline ? AlertKindView.Offline : a.Severity == AlertSeverity.Critical ? AlertKindView.Critical : AlertKindView.Warning;
        var label = kind switch { AlertKindView.Offline => "Offline", AlertKindView.Critical => "Critical", _ => "Warning" };
        string parameter = "—", reading = "—", threshold = "—";
        if (a.AlertType != AlertType.Offline && a.Parameter is { } p)
        {
            var unit = p == MeasuredParameter.Battery ? "%" : "";
            parameter = ParameterCatalog.Get(p).Short;
            reading = a.ReadingValue is { } rv ? Display.Value(p, rv) + unit : "—";
            threshold = a.ThresholdValue is { } tv ? (a.ThresholdIsMax ? "max " : "min ") + Display.Value(p, tv) + unit : "—";
        }
        // The owner is the person who last acted on the alert; an open alert has none yet.
        var actor = (a.Resolved ?? a.Acknowledged)?.UserId;
        var owner = actor is null ? "—" : userNames.GetValueOrDefault(actor, "—");
        return new AlertRowDto(a.AlertId, Display.Stamp(a.RaisedAt, now), kind, label, siteNames.GetValueOrDefault(a.SiteId, a.SiteId), a.SensorId, parameter, reading, threshold, owner, a.Status);
    }
}

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IAlertRepository _alerts;
    private readonly DataScope _scope;
    private readonly IClock _clock;

    public NotificationService(INotificationRepository notifications, IAlertRepository alerts, DataScope scope, IClock clock)
    {
        _notifications = notifications;
        _alerts = alerts;
        _scope = scope;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BellItemDto>> GetBellAsync(UserContext ctx, int max = 8)
    {
        var now = _clock.UtcNow;
        var sites = (await _scope.VisibleSitesAsync(ctx)).Select(s => s.SiteId).ToList();
        var alerts = (await _alerts.GetAllAsync(sites)).ToDictionary(a => a.AlertId);
        var result = new List<BellItemDto>();
        foreach (var n in (await _notifications.ForUserAsync(ctx.UserId)).OrderByDescending(n => n.CreatedAt))
        {
            if (!alerts.TryGetValue(n.AlertId, out var a)) continue; // not in this person's provinces
            var kind = a.AlertType == AlertType.Offline ? AlertKindView.Offline : a.Severity == AlertSeverity.Critical ? AlertKindView.Critical : AlertKindView.Warning;
            string title, detail;
            if (a.AlertType == AlertType.Offline)
            {
                title = $"{a.SensorId} is offline";
                detail = "No reading for 2 hours";
            }
            else
            {
                var p = a.Parameter ?? MeasuredParameter.Battery;
                title = a.AlertType == AlertType.Battery ? $"Battery low on {a.SensorId}" : $"{ParameterCatalog.Get(p).Short} {(a.ThresholdIsMax ? "above" : "below")} limit on {a.SensorId}";
                detail = $"{Display.Value(p, a.ReadingValue ?? 0)} against {(a.ThresholdIsMax ? "max" : "min")} {Display.Value(p, a.ThresholdValue ?? 0)}";
            }
            result.Add(new BellItemDto(n.AlertId, n.AlertId, title, detail, Display.DayLabel(n.CreatedAt, now), n.IsRead, kind));
            if (result.Count >= max) break;
        }
        return result;
    }

    // A notification is identified by its alert id: notifications/{uid}/{alert_id}.
    public Task MarkReadAsync(UserContext ctx, string alertId) => _notifications.MarkReadAsync(ctx.UserId, new[] { alertId }, _clock.UtcNow);

    public async Task MarkAllReadAsync(UserContext ctx)
    {
        var unread = (await _notifications.ForUserAsync(ctx.UserId)).Where(n => !n.IsRead).Select(n => n.AlertId).ToList();
        await _notifications.MarkReadAsync(ctx.UserId, unread, _clock.UtcNow);
    }
}

/// <summary>
/// Looks at the newest status and reading of every sensor and raises alerts. It never raises a second alert for a problem that already has an
/// Open or Acknowledged alert. A new alert and its notifications are written together. Only this class creates alerts and notifications.
/// </summary>
public sealed class AlertEvaluator : IAlertEvaluator
{
    private readonly ISensorRepository _sensors;
    private readonly SensorLookup _lookup;
    private readonly IAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly Analytics _analytics;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AlertEvaluator(ISensorRepository sensors, SensorLookup lookup, IAlertRepository alerts, IUserRepository users, Analytics analytics, IClock clock)
    {
        _sensors = sensors;
        _lookup = lookup;
        _alerts = alerts;
        _users = users;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<int> EvaluateAsync()
    {
        await _gate.WaitAsync(); // the background worker and a threshold save can call this at the same time
        try
        {
            var now = _clock.UtcNow;
            var sensors = (await _sensors.GetAllAsync()).Where(s => s.IsActive).ToList();
            var statuses = await _lookup.StatusesAsync(sensors);
            var latest = await _lookup.LatestReadingsAsync(sensors);
            var bands = await _analytics.AllBandsAsync();
            var active = (await _alerts.GetAllAsync()).Where(a => a.Status != AlertStatus.Resolved).ToList();
            var users = await _users.GetAllAsync();
            var raised = 0;

            foreach (var sensor in sensors)
            {
                var status = statuses[sensor.SensorId];

                if (OfflineRule.IsOffline(status?.LastCommunication, now))
                {
                    if (!active.Any(a => a.SensorId == sensor.SensorId && a.AlertType == AlertType.Offline))
                    {
                        await RaiseAsync(new Alert { SiteId = sensor.SiteId, SensorId = sensor.SensorId, AlertType = AlertType.Offline, Severity = AlertSeverity.Critical }, users, active, now);
                        raised++;
                    }
                    continue; // an offline sensor has no fresh reading to judge
                }

                if (latest[sensor.SensorId] is { } reading && bands.TryGetValue(sensor.SiteId, out var siteBands))
                {
                    foreach (var (parameter, band, above) in Analytics.Breaches(reading, siteBands))
                    {
                        if (active.Any(a => a.SensorId == sensor.SensorId && a.AlertType == AlertType.Breach && a.Parameter == parameter)) continue;
                        var limit = above ? band.MaxValue : band.MinValue;
                        var value = reading.ValueOf(parameter);
                        await RaiseAsync(new Alert
                        {
                            SiteId = sensor.SiteId, SensorId = sensor.SensorId, AlertType = AlertType.Breach, Parameter = parameter,
                            Severity = SeverityRule.For(value, limit), ReadingValue = value, ThresholdValue = limit
                        }, users, active, now);
                        raised++;
                    }
                }

                if (status is not null && BatteryRule.IsLow(status.BatteryPercent) && !active.Any(a => a.SensorId == sensor.SensorId && a.AlertType == AlertType.Battery))
                {
                    await RaiseAsync(new Alert
                    {
                        SiteId = sensor.SiteId, SensorId = sensor.SensorId, AlertType = AlertType.Battery, Parameter = MeasuredParameter.Battery,
                        Severity = AlertSeverity.Warning, ReadingValue = status.BatteryPercent, ThresholdValue = BatteryRule.AlertBelowPercent
                    }, users, active, now);
                    raised++;
                }
            }
            return raised;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Saves the alert together with one notification per person who can see the province (administrators see all) and wants that kind of message.</summary>
    private async Task RaiseAsync(Alert alert, IReadOnlyList<AppUser> users, List<Alert> active, DateTime now)
    {
        alert.AlertId = PushIds.At(now);
        alert.RaisedAt = now;
        alert.Status = AlertStatus.Open;

        var assigned = await _users.UserIdsForLocationAsync(alert.SiteId);
        var notifications = new List<Notification>();
        foreach (var user in users.Where(u => u.Status == AccountStatus.Active && (u.Role == UserRole.Administrator || assigned.Contains(u.UserId))))
        {
            var wants = alert.AlertType == AlertType.Offline ? user.NotifyOffline : user.NotifyBreach; // low battery follows the breach switch
            if (wants) notifications.Add(new Notification { UserId = user.UserId, AlertId = alert.AlertId, SiteId = alert.SiteId, CreatedAt = now, IsRead = false });
        }
        await _alerts.AddAsync(alert, notifications);
        active.Add(alert);
    }
}
