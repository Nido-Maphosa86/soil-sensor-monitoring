// What this file is for: the Sensors list, registering a new sensor, the Readings log with CSV export, and the Thresholds editor.
// How it fits in: Service folder. Controllers call these through interfaces; province filtering and the admin-only rules are enforced here, not only in the screens.
// Readings are read as key ranges per sensor. The log reads only the rows of the page it shows; Export CSV covers at most 7 days.
using System.Globalization;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class SensorService : ISensorService
{
    public const int PageSize = 10;
    public static readonly IReadOnlyList<string> StateFilters = new[] { "All statuses", "Online", "Offline", "Retired" };

    /// <summary>The note written on the open alerts of a sensor when it is retired.</summary>
    public const string RetiredNote = "Closed automatically: the sensor was retired.";

    private readonly DataScope _scope;
    private readonly ISensorRepository _sensors;
    private readonly ILocationRepository _locations;
    private readonly SensorLookup _lookup;
    private readonly IAlertRepository _alerts;
    private readonly NotificationLinks _links;
    private readonly IClock _clock;

    public SensorService(DataScope scope, ISensorRepository sensors, ILocationRepository locations, SensorLookup lookup, IAlertRepository alerts, NotificationLinks links, IClock clock)
    {
        _scope = scope;
        _sensors = sensors;
        _locations = locations;
        _lookup = lookup;
        _alerts = alerts;
        _links = links;
        _clock = clock;
    }

    public async Task<SensorsPageDto> GetPageAsync(UserContext ctx, string? siteId, string? search, string? state, int page)
    {
        var now = _clock.UtcNow;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var all = await _scope.VisibleSensorsAsync(ctx);
        var names = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var statuses = await _lookup.StatusesAsync(all);

        IEnumerable<Sensor> q = all;
        // a province outside the person's access gives an empty list, never the data
        if (!string.IsNullOrWhiteSpace(siteId)) q = ctx.CanSee(siteId) ? q.Where(s => s.SiteId == siteId) : q.Where(_ => false);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(s => s.SensorId.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        var rows = q.Select(s => SensorRowDto.From(s, names[s.SiteId], statuses[s.SensorId], now)).ToList();
        if (!string.IsNullOrWhiteSpace(state) && state != "All statuses") rows = rows.Where(r => r.StateLabel == state).ToList();

        var pageCount = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)PageSize));
        page = Math.Clamp(page, 1, pageCount);
        var slice = rows.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        // retired sensors are listed (so they can be reactivated or deleted) but not counted as offline
        var offline = all.Count(s => s.IsActive && OfflineRule.IsOffline(statuses[s.SensorId]?.LastCommunication, now));
        var retired = all.Count(s => !s.IsActive);
        var perSite = sites.Select(s => all.Count(x => x.SiteId == s.SiteId)).Distinct().ToList();
        var subtitle = $"{all.Count} registered units" + (perSite.Count == 1 && sites.Count > 1 ? $", {perSite[0]} in each province" : "") +
                       (offline > 0 ? $" · {offline} offline" : "") + (retired > 0 ? $" · {retired} retired" : "");
        var chips = new List<SensorChipDto> { new(null, sites.Count == 1 ? sites[0].Name : "All sites", all.Count) };
        if (sites.Count > 1) chips.AddRange(sites.Select(s => new SensorChipDto(s.SiteId, s.Name, all.Count(x => x.SiteId == s.SiteId))));

        return new SensorsPageDto(subtitle, chips, slice, page, pageCount, rows.Count, rows.Count == 0 ? 0 : (page - 1) * PageSize + 1, (page - 1) * PageSize + slice.Count);
    }

    public async Task<IReadOnlyList<(string SiteId, string Name)>> ProvincesAsync(UserContext ctx)
    {
        ctx.RequireAdmin();
        return (await _locations.GetAllAsync()).Select(s => (s.SiteId, s.Name)).ToList();
    }

    /// <summary>The form with sensible starting values: the next free code and the next free slave id.</summary>
    public async Task<RegisterSensorForm> NewFormAsync(UserContext ctx, string? siteId)
    {
        ctx.RequireAdmin();
        var sites = await _locations.GetAllAsync();
        var site = sites.FirstOrDefault(s => s.SiteId == siteId) ?? sites.First();
        var sensors = await _sensors.GetAllAsync();
        return new RegisterSensorForm
        {
            SiteId = site.SiteId, SensorId = NextCode(site, sensors), InstalledDate = Display.Local(_clock.UtcNow).Date, DepthCm = 15,
            ModbusSlaveId = NextSlave(site.SiteId, sensors), IncludeInAverage = true
        };
    }

    public async Task<RegisterResult> RegisterAsync(UserContext ctx, RegisterSensorForm form)
    {
        ctx.RequireAdmin();
        var errors = new Dictionary<string, string>();
        SlaveConflict? conflict = null;
        var sensors = await _sensors.GetAllAsync();

        var site = form.SiteId is null ? null : await _locations.GetAsync(form.SiteId);
        if (site is null) errors["SiteId"] = "Choose a province.";

        var code = (form.SensorId ?? "").Trim().ToUpperInvariant();
        if (!SensorCodeRule.IsValidShape(code)) errors["SensorId"] = "Use the province id, a dash and two digits (for example FS-06).";
        else if (site is not null && !SensorCodeRule.BelongsTo(code, site.SiteId)) errors["SensorId"] = $"The code must start with {site.SiteId}- for {site.Name}.";
        else if (sensors.Any(s => s.SensorId == code)) errors["SensorId"] = "A sensor with this code already exists.";

        conflict = CheckDetails(form, site, sensors, null, errors);
        if (errors.Count > 0) return new RegisterResult(false, errors, conflict);

        await _sensors.AddAsync(new Sensor
        {
            SensorId = code, SiteId = site!.SiteId, Name = $"Soil probe {code}", Model = "SG-RS485-7", IsActive = true, IncludeInAverage = form.IncludeInAverage,
            InstalledDate = form.InstalledDate!.Value.Date, DepthCm = form.DepthCm!.Value, ModbusSlaveId = form.ModbusSlaveId!.Value
        });
        return new RegisterResult(true, errors, null);
    }

    /// <summary>
    /// The checks shared by Register and Edit: installation date, depth, and a Modbus slave id that is unique within the site.
    /// self is the sensor being edited, so it does not clash with itself. Returns the clash, if any, for the one-click fix.
    /// </summary>
    private SlaveConflict? CheckDetails(RegisterSensorForm form, Site? site, IReadOnlyList<Sensor> sensors, string? self, Dictionary<string, string> errors)
    {
        if (form.InstalledDate is null) errors["InstalledDate"] = "Enter the installation date.";
        else if (form.InstalledDate.Value.Date > Display.Local(_clock.UtcNow).Date) errors["InstalledDate"] = "The installation date cannot be in the future.";

        if (form.DepthCm is null or < 1 or > 200) errors["DepthCm"] = "Enter a depth between 1 and 200 centimetres.";

        if (form.ModbusSlaveId is null or < 1 or > 247) errors["ModbusSlaveId"] = "Enter a slave id between 1 and 247.";
        else if (site is not null)
        {
            // a slave id must be unique within a location
            var clash = sensors.FirstOrDefault(s => s.SiteId == site.SiteId && s.SensorId != self && s.ModbusSlaveId == form.ModbusSlaveId);
            if (clash is not null)
            {
                errors["ModbusSlaveId"] = "Slave ID already in use.";
                return new SlaveConflict(clash.SensorId, clash.ModbusSlaveId, NextSlave(site.SiteId, sensors.Where(s => s.SensorId != self).ToList()));
            }
        }
        return null;
    }

    /// <summary>Gets a registered sensor for an administrator, or NotFound.</summary>
    private async Task<Sensor> RequireSensorAsync(UserContext ctx, string? sensorId)
    {
        ctx.RequireAdmin();
        return string.IsNullOrWhiteSpace(sensorId) ? throw new NotFoundException("That sensor was not found.")
            : await _sensors.GetAsync(sensorId) ?? throw new NotFoundException("That sensor was not found.");
    }

    public async Task<SensorEditDto> GetEditAsync(UserContext ctx, string sensorId)
    {
        var s = await RequireSensorAsync(ctx, sensorId);
        var site = await _locations.GetAsync(s.SiteId);
        var status = (await _lookup.StatusesAsync(new[] { s }))[s.SensorId];
        var alerts = (await _alerts.GetAllAsync(new[] { s.SiteId })).Where(a => a.SensorId == s.SensorId).ToList();
        var form = new RegisterSensorForm
        {
            SensorId = s.SensorId, SiteId = s.SiteId, InstalledDate = s.InstalledDate, DepthCm = s.DepthCm, ModbusSlaveId = s.ModbusSlaveId, IncludeInAverage = s.IncludeInAverage
        };
        return new SensorEditDto(form, site?.Name ?? s.SiteId, s.IsActive, Display.LastReading(status?.LastCommunication, _clock.UtcNow),
            alerts.Count(a => a.Status != AlertStatus.Resolved), alerts.Count);
    }

    public async Task<ChangeResult> UpdateAsync(UserContext ctx, RegisterSensorForm form)
    {
        var s = await RequireSensorAsync(ctx, form.SensorId);
        var errors = new Dictionary<string, string>();
        var conflict = CheckDetails(form, await _locations.GetAsync(s.SiteId), await _sensors.GetAllAsync(), s.SensorId, errors);
        if (errors.Count > 0) return new ChangeResult(false, errors, conflict);

        // the code, the site, the name and the model stay as they were
        s.InstalledDate = form.InstalledDate!.Value.Date;
        s.DepthCm = form.DepthCm!.Value;
        s.ModbusSlaveId = form.ModbusSlaveId!.Value;
        s.IncludeInAverage = form.IncludeInAverage;
        await _sensors.UpdateAsync(s);
        return ChangeResult.Ok;
    }

    public async Task SetActiveAsync(UserContext ctx, string sensorId, bool active)
    {
        var s = await RequireSensorAsync(ctx, sensorId);
        if (s.IsActive == active) return; // already in that state
        s.IsActive = active;
        await _sensors.UpdateAsync(s); // first, so the alert check no longer raises alerts for it

        if (active) return;
        // a retired sensor's open alerts would stay open forever, so they are resolved with a note (action and state in one update each)
        var now = _clock.UtcNow;
        foreach (var alert in (await _alerts.GetAllAsync(new[] { s.SiteId })).Where(a => a.SensorId == s.SensorId && a.Status != AlertStatus.Resolved))
        {
            AlertWorkflow.Resolve(alert, RetiredNote, ctx.UserId, now);
            await _alerts.SaveActionAsync(alert, resolved: true);
        }
    }

    public async Task<ChangeResult> DeleteAsync(UserContext ctx, string sensorId, string? confirm)
    {
        var s = await RequireSensorAsync(ctx, sensorId);
        if (!RemovalRule.CanDeleteSensor(s)) return ChangeResult.Fail("", "Retire the sensor before deleting it.");
        if (!RemovalRule.Confirmed(confirm, s.SensorId)) return ChangeResult.Fail("Confirm", $"Type {s.SensorId} to confirm.");

        var alertIds = (await _alerts.GetAllAsync(new[] { s.SiteId })).Where(a => a.SensorId == s.SensorId).Select(a => a.AlertId).ToList();
        await _sensors.DeleteAsync(s, alertIds, await _links.ForAlertsAsync(alertIds));
        return ChangeResult.Ok;
    }

    private static string NextCode(Site site, IReadOnlyList<Sensor> sensors)
    {
        var numbers = sensors.Where(s => s.SiteId == site.SiteId && s.SensorId.StartsWith(site.Prefix + "-"))
            .Select(s => int.TryParse(s.SensorId[(site.Prefix.Length + 1)..], out var n) ? n : 0).DefaultIfEmpty(0);
        return $"{site.Prefix}-{numbers.Max() + 1:00}";
    }

    private static int NextSlave(string siteId, IReadOnlyList<Sensor> sensors)
    {
        var used = sensors.Where(s => s.SiteId == siteId).Select(s => s.ModbusSlaveId).ToHashSet();
        var next = 1;
        while (used.Contains(next)) next++;
        return next;
    }
}

/// <summary>
/// The Readings log. The gateway writes a reading about every 6 seconds, so a page never reads the whole period: each sensor gives only the
/// rows the page shows (Firebase "the newest 14 before this key"), and Newer / Older move by key. Export CSV reads the whole period, so it is
/// limited to 7 days.
/// </summary>
public sealed class ReadingService : IReadingService
{
    public const int PageSize = 14;
    public const int MaxDays = 31;      // the longest period the log shows
    public const int MaxExportDays = 7; // the longest period Export CSV writes (about 100,000 rows per sensor)

    private readonly DataScope _scope;
    private readonly SensorLookup _lookup;
    private readonly IReadingRepository _readings;
    private readonly ICsvWriter _csv;
    private readonly IClock _clock;

    public ReadingService(DataScope scope, SensorLookup lookup, IReadingRepository readings, ICsvWriter csv, IClock clock)
    {
        _scope = scope;
        _lookup = lookup;
        _readings = readings;
        _csv = csv;
        _clock = clock;
    }

    // The dates in a filter are South African local times, as the person sees and types them.
    public ReadingFilter DefaultFilter()
    {
        var now = Display.Local(_clock.UtcNow);
        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        return new(minute.AddHours(-24), minute.AddMinutes(1), null, null);
    }

    public ReadingFilter FilterForRange(string? range, string? siteId = null)
    {
        var now = Display.Local(_clock.UtcNow);
        // the exports of the dashboard and the site page; the longest is 7 days, the most Export CSV writes
        return new(now - (range == "7d" ? TimeSpan.FromDays(7) : range == "15m" ? TimeSpan.FromMinutes(15) : TimeSpan.FromHours(24)), now, siteId, null);
    }

    /// <summary>What is wrong with the dates of a filter, or null when they are fine. maxDays is 31 for the log and 7 for Export CSV.</summary>
    public static string? CheckDates(ReadingFilter f, int maxDays)
    {
        if (f.From is null || f.To is null) return "Choose both a From and a To date.";
        if (f.From >= f.To) return "From must be before To.";
        if (f.To - f.From > TimeSpan.FromDays(maxDays))
            return maxDays == MaxExportDays
                ? $"Export CSV covers at most {MaxExportDays} days (a reading every 6 seconds is about 14,400 rows a day per sensor). Choose a shorter period."
                : $"The period can be at most {MaxDays} days. Choose a shorter period.";
        return null;
    }

    public async Task<ReadingsPageDto> GetPageAsync(UserContext ctx, ReadingFilter filter, string? exportError = null)
    {
        var sites = await _scope.VisibleSitesAsync(ctx);
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var visible = await _scope.VisibleSensorsAsync(ctx);
        var sensorIds = visible.Where(s => string.IsNullOrEmpty(filter.SiteId) || s.SiteId == filter.SiteId).Select(s => s.SensorId).ToList();
        var provinces = sites.Select(s => (s.SiteId, s.Name)).ToList();
        var scopeText = string.IsNullOrEmpty(filter.SiteId) ? (sites.Count == 3 ? "all provinces" : string.Join(", ", sites.Select(s => s.Name))) : siteNames.GetValueOrDefault(filter.SiteId, "");
        var caption = $"{Range(filter)} · {scopeText} · timestamps in South African Standard Time";

        var error = CheckDates(filter, MaxDays) ?? exportError;
        if (CheckDates(filter, MaxDays) is not null)
            return new ReadingsPageDto(filter, caption, new List<ReadingRowDto>(), null, null, false, error, provinces, sensorIds);

        // each sensor gives at most one page of rows; together they are cut to the page, newest first
        var sensors = Matching(visible, filter);
        var (fromUtc, toUtc) = Utc(filter);
        var found = await Task.WhenAll(sensors.Select(s => _readings.PageAsync(s.SiteId, s.SensorId, fromUtc, toUtc, filter.Before, filter.After, PageSize + 1)));
        var all = found.SelectMany(x => x).OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
        var more = all.Count > PageSize; // there is another page in the direction we are moving
        var page = (filter.After is not null ? all.Take(PageSize) : all.TakeLast(PageSize)).Reverse().ToList();

        var rows = page.Select(r => new ReadingRowDto(Display.Local(r.Reading.Timestamp).ToString("yyyy-MM-dd HH:mm:ss"), siteNames.GetValueOrDefault(r.Reading.SiteId, r.Reading.SiteId),
            r.Reading.SensorId, ParameterCatalog.Readings.Select(i => Display.Value(i.Parameter, r.Reading.ValueOf(i.Parameter))).ToList())).ToList();

        // Older continues below the last row shown; Newer continues above the first one. The newest page has no Newer.
        var older = page.Count > 0 && (filter.After is not null || more) ? page[^1].Key : null;
        var newer = page.Count > 0 && (filter.Before is not null || (filter.After is not null && more)) ? page[0].Key : null;
        return new ReadingsPageDto(filter, caption, rows, older, newer, filter.Before is null && filter.After is null, error, provinces, sensorIds);
    }

    public async Task<ReadingsExport> ExportCsvAsync(UserContext ctx, ReadingFilter filter)
    {
        if (CheckDates(filter, MaxExportDays) is { } error) return new ReadingsExport(null, error);
        var siteNames = (await _scope.VisibleSitesAsync(ctx)).ToDictionary(s => s.SiteId, s => s.Name);
        var sensors = Matching(await _scope.VisibleSensorsAsync(ctx), filter);
        var (fromUtc, toUtc) = Utc(filter);
        var found = await _lookup.ReadingsAsync(sensors, fromUtc, toUtc);
        var header = new[] { "Timestamp", "Province", "Sensor" }.Concat(ParameterCatalog.Readings.Select(i => $"{i.Short} ({i.Unit})"));
        var rows = found.Values.SelectMany(x => x).OrderByDescending(r => r.Timestamp).ThenBy(r => r.SensorId, StringComparer.Ordinal)
            .Select(r => new[] { Display.Local(r.Timestamp).ToString("yyyy-MM-dd HH:mm:ss"), siteNames[r.SiteId], r.SensorId }
                .Concat(ParameterCatalog.Readings.Select(i => r.ValueOf(i.Parameter).ToString(CultureInfo.InvariantCulture))));
        return new ReadingsExport(_csv.Write(header, rows), null);
    }

    /// <summary>The sensors the filter asks for, always limited to the person's provinces first.</summary>
    private static List<Sensor> Matching(IEnumerable<Sensor> visible, ReadingFilter f) =>
        visible.Where(s => string.IsNullOrEmpty(f.SiteId) || s.SiteId == f.SiteId).Where(s => string.IsNullOrEmpty(f.SensorId) || s.SensorId == f.SensorId).ToList();

    /// <summary>The filter's South African times as UTC.</summary>
    private static (DateTime From, DateTime To) Utc(ReadingFilter f) =>
        (DateTime.SpecifyKind(f.From!.Value - Display.SouthAfrica, DateTimeKind.Utc), DateTime.SpecifyKind(f.To!.Value - Display.SouthAfrica, DateTimeKind.Utc));

    private static string Range(ReadingFilter f)
    {
        if (f.From is null || f.To is null) return "Last 24 hours";
        var a = f.From.Value;
        var b = f.To.Value;
        if (a.Date == b.Date) return $"{a:HH:mm} – {b:HH:mm}, {b.ToString("d MMMM yyyy", Display.Culture)}";
        return $"{a.ToString("d MMM HH:mm", Display.Culture)} – {b.ToString("d MMM yyyy HH:mm", Display.Culture)}";
    }
}

public sealed class ThresholdService : IThresholdService
{
    private readonly DataScope _scope;
    private readonly IThresholdRepository _thresholds;
    private readonly ILocationRepository _locations;
    private readonly ISensorRepository _sensors;
    private readonly IUserRepository _users;
    private readonly SiteMetrics _metrics;
    private readonly IAlertEvaluator _evaluator;
    private readonly IClock _clock;

    public ThresholdService(DataScope scope, IThresholdRepository thresholds, ILocationRepository locations, ISensorRepository sensors, IUserRepository users,
        SiteMetrics metrics, IAlertEvaluator evaluator, IClock clock)
    {
        _scope = scope;
        _thresholds = thresholds;
        _locations = locations;
        _sensors = sensors;
        _users = users;
        _metrics = metrics;
        _evaluator = evaluator;
        _clock = clock;
    }

    /// <summary>The first province, used when no province is chosen yet. Empty when no site exists yet.</summary>
    public async Task<string> DefaultSiteIdAsync(UserContext ctx)
    {
        ctx.RequireAdmin();
        return (await _locations.GetAllAsync()).FirstOrDefault()?.SiteId ?? "";
    }

    public Task<ThresholdEditorDto> GetEditorAsync(UserContext ctx, string siteId) => BuildAsync(ctx, siteId, null, null, null, null);

    /// <summary>Validates every band (min below max) and a basis, then saves all bands together in one update, or none if one is wrong.</summary>
    public async Task<SaveThresholdsResult> SaveAsync(UserContext ctx, string siteId, string? basis, IReadOnlyDictionary<MeasuredParameter, (string? Min, string? Max)> input)
    {
        ctx.RequireAdmin();
        var site = await _scope.RequireSiteAsync(ctx, siteId);
        var errors = new Dictionary<MeasuredParameter, string>();
        var parsed = new Dictionary<MeasuredParameter, (double Min, double Max)>();
        string? basisError = null;

        foreach (var info in ParameterCatalog.Readings)
        {
            input.TryGetValue(info.Parameter, out var pair);
            if (!double.TryParse(pair.Min, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ||
                !double.TryParse(pair.Max, NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
            {
                errors[info.Parameter] = "Enter a number for both limits.";
                continue;
            }
            try
            {
                ThresholdRule.EnsureValid(min, max, string.IsNullOrWhiteSpace(basis) ? "x" : basis); // the basis is checked once, below
                parsed[info.Parameter] = (min, max);
            }
            catch (DomainException e) { errors[info.Parameter] = e.Message; }
        }
        if (string.IsNullOrWhiteSpace(basis)) basisError = "A basis is required (for example the crop the bands are based on).";
        else if (basis.Trim().Length > 60) basisError = "The basis can be at most 60 characters.";

        if (errors.Count > 0 || basisError is not null) return new SaveThresholdsResult(false, await BuildAsync(ctx, siteId, input, errors, basis, basisError));

        var now = _clock.UtcNow;
        await _thresholds.SaveAsync(parsed.Select(p => new Threshold
        {
            SiteId = site.SiteId, Parameter = p.Key, MinValue = p.Value.Min, MaxValue = p.Value.Max, Basis = basis!.Trim(), UpdatedBy = ctx.UserId, UpdatedAt = now
        }));
        await _evaluator.EvaluateAsync(); // new bands may create alerts straight away
        return new SaveThresholdsResult(true, await BuildAsync(ctx, siteId, null, null, null, null));
    }

    public async Task ResetToDefaultsAsync(UserContext ctx, string siteId)
    {
        ctx.RequireAdmin();
        var site = await _scope.RequireSiteAsync(ctx, siteId);
        var now = _clock.UtcNow;
        await _thresholds.SaveAsync(ParameterCatalog.Readings.Select(i => new Threshold
        {
            SiteId = site.SiteId, Parameter = i.Parameter, MinValue = i.DefaultMin, MaxValue = i.DefaultMax, Basis = site.Crop, UpdatedBy = ctx.UserId, UpdatedAt = now
        }));
        await _evaluator.EvaluateAsync();
    }

    private async Task<ThresholdEditorDto> BuildAsync(UserContext ctx, string siteId, IReadOnlyDictionary<MeasuredParameter, (string? Min, string? Max)>? typed,
        IReadOnlyDictionary<MeasuredParameter, string>? errors, string? typedBasis, string? basisError)
    {
        ctx.RequireAdmin();
        var site = await _scope.RequireSiteAsync(ctx, siteId);
        var bands = (await _thresholds.GetAllAsync()).Where(t => t.SiteId == siteId).ToDictionary(t => t.Parameter);
        var siteSensors = (await _sensors.GetAllAsync()).Where(s => s.SiteId == siteId).ToList();
        var current = await _metrics.CurrentReadingsAsync(siteSensors);
        var cards = new List<ThresholdCardDto>();
        foreach (var info in ParameterCatalog.Readings)
        {
            var band = bands.GetValueOrDefault(info.Parameter) ?? new Threshold { SiteId = siteId, Parameter = info.Parameter, MinValue = info.DefaultMin, MaxValue = info.DefaultMax };
            var now = SiteMetrics.Mean(current, info.Parameter);
            var position = (int)Math.Round(Math.Clamp(18 + 58 * (now - band.MinValue) / (band.MaxValue - band.MinValue), 2, 96));
            var min = Display.Value(info.Parameter, band.MinValue);
            var max = Display.Value(info.Parameter, band.MaxValue);
            if (typed is not null && typed.TryGetValue(info.Parameter, out var t)) { min = t.Min ?? ""; max = t.Max ?? ""; }
            cards.Add(new ThresholdCardDto(info.Parameter, info.Label, info.Unit, min, max, Display.Value(info.Parameter, now), position, info.Colour, errors?.GetValueOrDefault(info.Parameter)));
        }

        var last = bands.Values.OrderByDescending(b => b.UpdatedAt).FirstOrDefault();
        var who = last is null ? "" : (await _users.GetAsync(last.UpdatedBy))?.Name ?? "an administrator";
        var caption = last is null
            ? $"{siteSensors.Count} sensors · using the default bands"
            : $"{siteSensors.Count} sensors · last edited {Display.LongDateLocal(last.UpdatedAt).Replace($" {Display.Local(last.UpdatedAt).Year}", "")} by {who}";
        var all = (await _locations.GetAllAsync()).Select(s => (s.SiteId, s.Name)).ToList();
        return new ThresholdEditorDto(siteId, all, site.Crop, typedBasis ?? last?.Basis ?? site.Crop, caption, cards, basisError);
    }
}
