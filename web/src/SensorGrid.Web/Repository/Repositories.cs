// What this file is for: every repository, written ONCE against IRealtimeDatabaseStore with the exact paths and field names of the tree.
// How it fits in: Repository folder. They only use the store interface, so the paths and field names live here and nowhere else.
// Times are UTC milliseconds, dates are yyyy-MM-dd and empty values are not stored. Readings are read exactly as the sensor gateway writes them.
using System.Text.Json.Nodes;
using SensorGrid.Domain;
using static SensorGrid.Repository.TreeJson;

namespace SensorGrid.Repository;

public sealed class LocationRepository : ILocationRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public LocationRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct = default)
    {
        var node = await _store.GetAsync("locations", ct) as JsonObject;
        return node is null ? new List<Site>() : node.Select(kv => Map(kv.Key, kv.Value)).OrderBy(s => s.SiteId, StringComparer.Ordinal).ToList();
    }

    public async Task<Site?> GetAsync(string locationId, CancellationToken ct = default)
    {
        var node = await _store.GetAsync($"locations/{locationId}", ct);
        return node is null ? null : Map(locationId, node);
    }

    public Task SaveAsync(Site s, CancellationToken ct = default)
    {
        var o = new JsonObject();
        Put(o, "name", s.Name);
        Put(o, "description", s.Description);
        Put(o, "crop", s.Crop);
        Put(o, "latitude", s.Latitude);
        Put(o, "longitude", s.Longitude);
        Put(o, "planting_date", DateText(s.PlantingDate));
        Put(o, "created_at", s.CreatedAt);
        return _store.SetAsync($"locations/{s.SiteId}", o, ct);
    }

    public Task DeleteAsync(string locationId, IEnumerable<string> userIds, IEnumerable<(string UserId, string AlertId)> notifications, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(locationId)) throw new ArgumentException("A site id is required.", nameof(locationId)); // a blank id would delete whole branches
        // null deletes; everything goes in one update, so the site is never half deleted
        var updates = new Dictionary<string, JsonNode?>
        {
            [$"locations/{locationId}"] = null,
            [$"thresholds/{locationId}"] = null,
            [$"alerts/{locationId}"] = null,
            [$"alert_actions/{locationId}"] = null,
            [$"location_users/{locationId}"] = null
        };
        foreach (var uid in userIds.Distinct()) updates[$"user_locations/{uid}/{locationId}"] = null;
        foreach (var (uid, alertId) in notifications.Distinct()) updates[$"notifications/{uid}/{alertId}"] = null;
        return _store.UpdateAsync("", updates, ct);
    }

    private static Site Map(string id, JsonNode? n) => new()
    {
        SiteId = id, Name = Str(n, "name", id), Description = Str(n, "description"), Crop = Str(n, "crop", "Pigeon pea"),
        Latitude = Num(n, "latitude") ?? 0, Longitude = Num(n, "longitude") ?? 0, PlantingDate = ParseDate(Str(n, "planting_date")), CreatedAt = Time(n, "created_at") ?? default
    };
}

public sealed class UserRepository : IUserRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public UserRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _store.GetAsync("users", ct) as JsonObject;
        var links = await _store.GetAsync("user_locations", ct) as JsonObject;
        if (users is null) return new List<AppUser>();
        return users.Select(kv => Map(kv.Key, kv.Value, links?[kv.Key] as JsonObject)).ToList();
    }

    public async Task<AppUser?> GetAsync(string uid, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        var node = await _store.GetAsync($"users/{uid}", ct);
        if (node is null) return null;
        return Map(uid, node, await _store.GetAsync($"user_locations/{uid}", ct) as JsonObject);
    }

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task SaveAsync(AppUser u, CancellationToken ct = default)
    {
        var before = await _store.GetAsync($"user_locations/{u.UserId}", ct) as JsonObject;
        var o = new JsonObject();
        Put(o, "name", u.Name);
        Put(o, "email", u.Email);
        Put(o, "role", RoleText(u.Role));
        Put(o, "account_status", u.Status.ToString());
        Put(o, "created_at", u.CreatedAt);
        Put(o, "notify_breach", u.NotifyBreach);
        Put(o, "notify_offline", u.NotifyOffline);
        if (u.LastActiveAt is { } last) Put(o, "last_active_at", last);

        // user_locations and location_users are always written together, in one update.
        var updates = new Dictionary<string, JsonNode?> { [$"users/{u.UserId}"] = o };
        foreach (var loc in u.SiteIds.Distinct())
        {
            updates[$"user_locations/{u.UserId}/{loc}"] = true;
            updates[$"location_users/{loc}/{u.UserId}"] = true;
        }
        if (before is not null)
            foreach (var old in before.Select(kv => kv.Key).Where(k => !u.SiteIds.Contains(k)))
            {
                updates[$"user_locations/{u.UserId}/{old}"] = null;
                updates[$"location_users/{old}/{u.UserId}"] = null;
            }
        await _store.UpdateAsync("", updates, ct);
    }

    public Task TouchLastActiveAsync(string uid, DateTime utc, CancellationToken ct = default) =>
        _store.SetAsync($"users/{uid}/last_active_at", JsonValue.Create(ToMs(utc)), ct);

    public async Task<IReadOnlyList<string>> UserIdsForLocationAsync(string locationId, CancellationToken ct = default)
    {
        var node = await _store.GetAsync($"location_users/{locationId}", ct) as JsonObject;
        return node is null ? new List<string>() : node.Select(kv => kv.Key).ToList();
    }

    private static AppUser Map(string uid, JsonNode? n, JsonObject? links) => new()
    {
        UserId = uid, Name = Str(n, "name"), Email = Str(n, "email"), Role = ParseRole(Str(n, "role")),
        Status = ParseEnum(Str(n, "account_status"), AccountStatus.Deactivated), CreatedAt = Time(n, "created_at") ?? default,
        LastActiveAt = Time(n, "last_active_at"), NotifyBreach = Bool(n, "notify_breach", true), NotifyOffline = Bool(n, "notify_offline", true),
        SiteIds = links is null ? new List<string>() : links.Select(kv => kv.Key).ToList()
    };
}

public sealed class SensorRepository : ISensorRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public SensorRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken ct = default)
    {
        var branch = await _store.GetAsync("sensors", ct) as JsonObject;
        var list = new List<Sensor>();
        if (branch is null) return list;
        foreach (var (loc, sensors) in branch)
            if (sensors is JsonObject so)
                foreach (var (id, node) in so) list.Add(Map(loc, id, node));
        return list.OrderBy(s => s.SensorId, StringComparer.Ordinal).ToList();
    }

    public async Task<Sensor?> GetAsync(string sensorId, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(s => s.SensorId == sensorId);

    public Task AddAsync(Sensor s, CancellationToken ct = default) =>
        // The lookup goes in the same update, so the security rules always know the sensor's province.
        _store.UpdateAsync("", new Dictionary<string, JsonNode?>
        {
            [$"sensors/{s.SiteId}/{s.SensorId}"] = ToJson(s),
            [$"sensor_locations/{s.SensorId}"] = s.SiteId
        }, ct);

    public Task UpdateAsync(Sensor s, CancellationToken ct = default) => _store.SetAsync($"sensors/{s.SiteId}/{s.SensorId}", ToJson(s), ct);

    public Task DeleteAsync(Sensor s, IEnumerable<string> alertIds, IEnumerable<(string UserId, string AlertId)> notifications, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.SiteId) || string.IsNullOrWhiteSpace(s.SensorId)) throw new ArgumentException("A sensor code and site are required.", nameof(s));
        // null deletes; readings/{sensor_id} is left alone, so the gateway's data is never lost
        var updates = new Dictionary<string, JsonNode?>
        {
            [$"sensors/{s.SiteId}/{s.SensorId}"] = null,
            [$"sensor_locations/{s.SensorId}"] = null
        };
        foreach (var id in alertIds.Distinct())
        {
            updates[$"alerts/{s.SiteId}/{id}"] = null;
            updates[$"alert_actions/{s.SiteId}/{id}"] = null;
        }
        foreach (var (uid, alertId) in notifications.Distinct()) updates[$"notifications/{uid}/{alertId}"] = null;
        return _store.UpdateAsync("", updates, ct);
    }

    private static JsonObject ToJson(Sensor s)
    {
        var o = new JsonObject();
        Put(o, "name", s.Name);
        Put(o, "model", s.Model);
        Put(o, "is_active", s.IsActive);
        Put(o, "include_in_average", s.IncludeInAverage);
        Put(o, "installation_date", DateText(s.InstalledDate));
        Put(o, "depth_cm", s.DepthCm);
        Put(o, "modbus_slave_id", s.ModbusSlaveId);
        return o;
    }

    private static Sensor Map(string loc, string id, JsonNode? n) => new()
    {
        SiteId = loc, SensorId = id, Name = Str(n, "name", id), Model = Str(n, "model"), IsActive = Bool(n, "is_active", true),
        IncludeInAverage = Bool(n, "include_in_average", true), InstalledDate = ParseDate(Str(n, "installation_date")), DepthCm = (int)(Long(n, "depth_cm") ?? 15),
        ModbusSlaveId = (int)(Long(n, "modbus_slave_id") ?? 0)
    };
}

public sealed class SensorStatusRepository : ISensorStatusRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public SensorStatusRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<SensorStatus?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default)
    {
        // The gateway overwrites "latest" with every row, so its timestamp is when the sensor last reported.
        var node = await _store.GetAsync($"readings/{sensorId}/latest", ct);
        return Time(node, "timestamp") is { } at ? new SensorStatus { SiteId = locationId, SensorId = sensorId, LastCommunication = at } : null;
    }
}

public sealed class ReadingRepository : IReadingRepository
{
    private readonly IRealtimeDatabaseStore _store;
    private readonly double _moistureDivisor;

    public ReadingRepository(IRealtimeDatabaseStore store, SensorGridSettings settings)
    {
        _store = store;
        _moistureDivisor = settings.GatewayMoistureDivisor > 0 ? settings.GatewayMoistureDivisor : 1;
    }

    public async Task<IReadOnlyList<Reading>> QueryAsync(string locationId, string sensorId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // The keys are push IDs, which start with the time they were made, so a period is a key range:
        // from the first possible ID at fromUtc to the last possible ID at toUtc.
        var rows = await _store.QueryByKeyAsync($"readings/{sensorId}/history",
            PushIds.TimePrefix(fromUtc), PushIds.TimePrefix(toUtc) + new string(PushIds.LastChar, 12), ct: ct);
        return rows.Select(r => Map(locationId, sensorId, r.Value)).ToList();
    }

    public async Task<IReadOnlyList<(string Key, Reading Reading)>> PageAsync(string locationId, string sensorId, DateTime fromUtc, DateTime toUtc,
        string? beforeKey, string? afterKey, int count, CancellationToken ct = default)
    {
        var first = PushIds.TimePrefix(fromUtc);
        var last = PushIds.TimePrefix(toUtc) + new string(PushIds.LastChar, 12);
        IReadOnlyList<KeyValuePair<string, JsonNode?>> rows;
        if (afterKey is not null)
        {
            // the key range is inclusive, so one extra row is asked for and the cursor row itself is dropped
            var start = string.CompareOrdinal(afterKey, first) > 0 ? afterKey : first;
            rows = await _store.QueryByKeyAsync($"readings/{sensorId}/history", start, last, ct: ct, limitToFirst: count + 1);
            rows = rows.Where(r => string.CompareOrdinal(r.Key, afterKey) > 0).Take(count).ToList();
        }
        else
        {
            var end = beforeKey is not null && string.CompareOrdinal(beforeKey, last) < 0 ? beforeKey : last;
            rows = await _store.QueryByKeyAsync($"readings/{sensorId}/history", first, end, count + 1, ct);
            rows = rows.Where(r => beforeKey is null || string.CompareOrdinal(r.Key, beforeKey) < 0).TakeLast(count).ToList();
        }
        return rows.Select(r => (r.Key, Map(locationId, sensorId, r.Value))).ToList();
    }

    public async Task<Reading?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default)
    {
        var node = await _store.GetAsync($"readings/{sensorId}/latest", ct);
        return node is null ? null : Map(locationId, sensorId, node);
    }

    // The gateway's own field names. "device_id" and "humidity" are in the row too but not used.
    private Reading Map(string loc, string sensor, JsonNode? n) => new()
    {
        SiteId = loc, SensorId = sensor, Timestamp = Time(n, "timestamp") ?? default,
        Moisture = Math.Round((Num(n, "moisture") ?? 0) / _moistureDivisor, 1), Temperature = Num(n, "temperature") ?? 0, Ph = Num(n, "ph") ?? 0,
        Conductivity = Num(n, "ec") ?? 0, Nitrogen = Num(n, "nitrogen") ?? 0, Phosphorus = Num(n, "phosphorus") ?? 0, Potassium = Num(n, "potassium") ?? 0
    };
}

public sealed class ThresholdRepository : IThresholdRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public ThresholdRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Threshold>> GetAllAsync(CancellationToken ct = default)
    {
        var branch = await _store.GetAsync("thresholds", ct) as JsonObject;
        var list = new List<Threshold>();
        if (branch is null) return list;
        foreach (var (loc, parameters) in branch)
            if (parameters is JsonObject po)
                foreach (var (key, node) in po)
                    if (ParseParameter(key) is { } p)
                        list.Add(new Threshold
                        {
                            SiteId = loc, Parameter = p, MinValue = Num(node, "min_value") ?? 0, MaxValue = Num(node, "max_value") ?? 0,
                            Basis = Str(node, "basis"), UpdatedBy = Str(node, "updated_by"), UpdatedAt = Time(node, "updated_at") ?? default
                        });
        return list;
    }

    public Task SaveAsync(IEnumerable<Threshold> thresholds, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, JsonNode?>();
        foreach (var t in thresholds)
        {
            var o = new JsonObject();
            Put(o, "min_value", t.MinValue);
            Put(o, "max_value", t.MaxValue);
            Put(o, "basis", t.Basis);
            Put(o, "updated_by", t.UpdatedBy);
            Put(o, "updated_at", t.UpdatedAt);
            updates[$"thresholds/{t.SiteId}/{Key(t.Parameter)}"] = o;
        }
        return _store.UpdateAsync("", updates, ct);
    }
}

public sealed class AlertRepository : IAlertRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public AlertRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Alert>> GetAllAsync(IEnumerable<string>? locationIds = null, CancellationToken ct = default)
    {
        var ids = locationIds?.ToList() ?? ((await _store.GetAsync("locations", ct)) as JsonObject)?.Select(kv => kv.Key).ToList() ?? new List<string>();
        // every province's alerts and alert actions are asked for at the same time, instead of one after another
        var perProvince = await Task.WhenAll(ids.Select(async loc =>
        {
            var alertsTask = _store.GetAsync($"alerts/{loc}", ct);
            var actionsTask = _store.GetAsync($"alert_actions/{loc}", ct);
            var alerts = await alertsTask as JsonObject;
            var actions = await actionsTask as JsonObject;
            return alerts is null ? new List<Alert>() : alerts.Select(kv => Map(loc, kv.Key, kv.Value, actions?[kv.Key] as JsonObject)).ToList();
        }));
        return perProvince.SelectMany(x => x).ToList();
    }

    public async Task<Alert?> GetAsync(string locationId, string alertId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(alertId)) return null; // a blank id would read the whole province node
        var node = await _store.GetAsync($"alerts/{locationId}/{alertId}", ct);
        if (node is null) return null;
        return Map(locationId, alertId, node, await _store.GetAsync($"alert_actions/{locationId}/{alertId}", ct) as JsonObject);
    }

    public Task AddAsync(Alert a, IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        var o = new JsonObject();
        Put(o, "sensor_id", a.SensorId);
        Put(o, "alert_type", a.AlertType.ToString());
        if (a.Parameter is { } p) Put(o, "parameter", Key(p));
        Put(o, "measured_value", a.ReadingValue);
        Put(o, "threshold_value", a.ThresholdValue);
        Put(o, "severity", a.Severity.ToString());
        Put(o, "state", a.Status.ToString());
        Put(o, "raised_at", a.RaisedAt);

        // the alert and its notifications are written together
        var updates = new Dictionary<string, JsonNode?> { [$"alerts/{a.SiteId}/{a.AlertId}"] = o };
        foreach (var n in notifications)
        {
            var no = new JsonObject();
            Put(no, "location_id", n.SiteId);
            Put(no, "created_at", n.CreatedAt);
            Put(no, "is_read", n.IsRead);
            updates[$"notifications/{n.UserId}/{n.AlertId}"] = no;
        }
        return _store.UpdateAsync("", updates, ct);
    }

    public Task SaveActionAsync(Alert a, bool resolved, CancellationToken ct = default)
    {
        var action = resolved ? a.Resolved : a.Acknowledged;
        if (action is null) throw new DomainException("There is no action to save.");
        var o = new JsonObject();
        Put(o, "user_id", action.UserId);
        Put(o, "note", action.Note);
        Put(o, "acted_at", action.ActedAt);
        // ONE update writes the action AND the new state, so they can never disagree.
        return _store.UpdateAsync("", new Dictionary<string, JsonNode?>
        {
            [$"alert_actions/{a.SiteId}/{a.AlertId}/{(resolved ? "resolved" : "acknowledged")}"] = o,
            [$"alerts/{a.SiteId}/{a.AlertId}/state"] = a.Status.ToString()
        }, ct);
    }

    private static Alert Map(string loc, string id, JsonNode? n, JsonObject? actions) => new()
    {
        SiteId = loc, AlertId = id, SensorId = Str(n, "sensor_id"), AlertType = ParseEnum(Str(n, "alert_type"), AlertType.Breach),
        Parameter = ParseParameter(Str(n, "parameter")), ReadingValue = Num(n, "measured_value"), ThresholdValue = Num(n, "threshold_value"),
        Severity = ParseEnum(Str(n, "severity"), AlertSeverity.Warning), Status = ParseEnum(Str(n, "state"), AlertStatus.Open), RaisedAt = Time(n, "raised_at") ?? default,
        Acknowledged = Action(actions?["acknowledged"]), Resolved = Action(actions?["resolved"])
    };

    private static AlertAction? Action(JsonNode? n) => n is null ? null : new AlertAction { UserId = Str(n, "user_id"), Note = Str(n, "note"), ActedAt = Time(n, "acted_at") ?? default };
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public NotificationRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Notification>> ForUserAsync(string uid, CancellationToken ct = default)
    {
        var node = await _store.GetAsync($"notifications/{uid}", ct) as JsonObject;
        return node is null
            ? new List<Notification>()
            : node.Select(kv => new Notification
            {
                UserId = uid, AlertId = kv.Key, SiteId = Str(kv.Value, "location_id"), CreatedAt = Time(kv.Value, "created_at") ?? default,
                IsRead = Bool(kv.Value, "is_read"), ReadAt = Time(kv.Value, "read_at")
            }).ToList();
    }

    public Task MarkReadAsync(string uid, IEnumerable<string> alertIds, DateTime readAtUtc, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, JsonNode?>();
        foreach (var id in alertIds)
        {
            updates[$"notifications/{uid}/{id}/is_read"] = true;
            updates[$"notifications/{uid}/{id}/read_at"] = JsonValue.Create(ToMs(readAtUtc));
        }
        return updates.Count == 0 ? Task.CompletedTask : _store.UpdateAsync("", updates, ct);
    }
}

public sealed class ReportRepository : IReportRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public ReportRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default)
    {
        var reports = await _store.GetAsync("reports", ct) as JsonObject;
        var links = await _store.GetAsync("report_locations", ct) as JsonObject;
        return reports is null ? new List<Report>() : reports.Select(kv => Map(kv.Key, kv.Value, links?[kv.Key] as JsonObject)).ToList();
    }

    public async Task<Report?> GetAsync(string reportId, CancellationToken ct = default)
    {
        var node = await _store.GetAsync($"reports/{reportId}", ct);
        return node is null ? null : Map(reportId, node, await _store.GetAsync($"report_locations/{reportId}", ct) as JsonObject);
    }

    public Task AddAsync(Report r, CancellationToken ct = default)
    {
        var o = new JsonObject();
        Put(o, "generated_by", r.GeneratedBy);
        Put(o, "name", r.Name);
        Put(o, "format", FormatText(r.Format));
        Put(o, "report_type", KindText(r.Kind));
        Put(o, "period_start", DateText(r.PeriodStart));
        Put(o, "period_end", DateText(r.PeriodEnd));
        Put(o, "generated_at", r.GeneratedAt);
        var updates = new Dictionary<string, JsonNode?> { [$"reports/{r.ReportId}"] = o };
        foreach (var loc in r.SiteIds) updates[$"report_locations/{r.ReportId}/{loc}"] = true;
        return _store.UpdateAsync("", updates, ct);
    }

    private static Report Map(string id, JsonNode? n, JsonObject? links) => new()
    {
        ReportId = id, GeneratedBy = Str(n, "generated_by"), Name = Str(n, "name"), Format = ParseFormat(Str(n, "format")), Kind = ParseKind(Str(n, "report_type")),
        PeriodStart = ParseDate(Str(n, "period_start")), PeriodEnd = ParseDate(Str(n, "period_end")), GeneratedAt = Time(n, "generated_at") ?? default,
        SiteIds = links is null ? new List<string>() : links.Select(kv => kv.Key).ToList()
    };
}

public sealed class DailyStatsRepository : IDailyStatsRepository
{
    private readonly IRealtimeDatabaseStore _store;

    public DailyStatsRepository(IRealtimeDatabaseStore store) => _store = store;

    public async Task<IReadOnlyList<DailyStats>> GetRangeAsync(string sensorId, DateTime firstDay, DateTime lastDay, CancellationToken ct = default)
    {
        // the keys are yyyy-MM-dd, so they sort by date and a period is a key range
        var rows = await _store.QueryByKeyAsync($"daily_stats/{sensorId}", DateText(firstDay), DateText(lastDay), ct: ct);
        return rows.Select(r => Map(sensorId, r.Key, r.Value)).ToList();
    }

    public Task SaveAsync(DailyStats d, CancellationToken ct = default)
    {
        var o = new JsonObject();
        Put(o, "readings", d.Readings); // stored even when 0, so a silent day is not worked out again
        Put(o, "in_range", d.InRange);
        foreach (var (p, (min, sum, max)) in d.Values)
        {
            Put(o, $"{Key(p)}_min", min);
            Put(o, $"{Key(p)}_sum", sum);
            Put(o, $"{Key(p)}_max", max);
        }
        if (d.FirstAt is { } first) Put(o, "first_at", first);
        if (d.LastAt is { } last) Put(o, "last_at", last);
        Put(o, "longest_gap_ms", (long)d.LongestGap.TotalMilliseconds);
        Put(o, "offline_ms", (long)d.Offline.TotalMilliseconds);
        return _store.SetAsync($"daily_stats/{d.SensorId}/{DateText(d.Day)}", o, ct);
    }

    private static DailyStats Map(string sensorId, string day, JsonNode? n)
    {
        var d = new DailyStats
        {
            SensorId = sensorId, Day = ParseDate(day), Readings = (int)(Long(n, "readings") ?? 0), InRange = (int)(Long(n, "in_range") ?? 0),
            FirstAt = Time(n, "first_at"), LastAt = Time(n, "last_at"),
            LongestGap = TimeSpan.FromMilliseconds(Long(n, "longest_gap_ms") ?? 0), Offline = TimeSpan.FromMilliseconds(Long(n, "offline_ms") ?? 0)
        };
        foreach (var p in Enum.GetValues<MeasuredParameter>())
            if (Num(n, $"{Key(p)}_sum") is { } sum)
                d.Values[p] = (Num(n, $"{Key(p)}_min") ?? 0, sum, Num(n, $"{Key(p)}_max") ?? 0);
        return d;
    }
}
