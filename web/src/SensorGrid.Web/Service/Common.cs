// What this file is for: shared helpers for the services: who is asking (UserContext), the province access check (DataScope), parameter names and units,
// display formatting (South African time), live sensor data lookups and the "in range" maths.
// How it fits in: Service folder. Every service uses these so province filtering, time zone and number formats are done in ONE place.
using System.Globalization;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

/// <summary>Thrown when someone asks for something their role or provinces do not allow.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have access to this.") : base(message) { }
}

/// <summary>Thrown when an item does not exist or is outside the person's provinces (the two cases look the same on purpose).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message = "Not found.") : base(message) { }
}

/// <summary>Who is making the request: id, name, role and assigned provinces. Built from the sign-in cookie.</summary>
public sealed record UserContext(string UserId, string Name, UserRole Role, IReadOnlyCollection<string> SiteIds)
{
    public bool IsAdmin => Role == UserRole.Administrator;

    /// <summary>Administrators see every province; others only the ones assigned to them.</summary>
    public bool CanSee(string siteId) => IsAdmin || SiteIds.Contains(siteId);

    public void RequireAdmin()
    {
        if (!IsAdmin) throw new ForbiddenException("Only an administrator can do this.");
    }
}

/// <summary>
/// The single place that decides which provinces and sensors a person may see. The web server's database token skips Firebase Security Rules,
/// so every service goes through here BEFORE it reads or writes.
/// </summary>
public sealed class DataScope
{
    private readonly ILocationRepository _locations;
    private readonly ISensorRepository _sensors;

    public DataScope(ILocationRepository locations, ISensorRepository sensors)
    {
        _locations = locations;
        _sensors = sensors;
    }

    public async Task<IReadOnlyList<Site>> VisibleSitesAsync(UserContext ctx) =>
        (await _locations.GetAllAsync()).Where(s => ctx.CanSee(s.SiteId)).ToList();

    public async Task<IReadOnlyList<Sensor>> VisibleSensorsAsync(UserContext ctx) =>
        (await _sensors.GetAllAsync()).Where(s => ctx.CanSee(s.SiteId)).OrderBy(s => s.SensorId, StringComparer.Ordinal).ToList();

    /// <summary>Gets a site or throws NotFound when it does not exist or is outside the person's provinces.</summary>
    public async Task<Site> RequireSiteAsync(UserContext ctx, string siteId) =>
        ctx.CanSee(siteId) && await _locations.GetAsync(siteId) is { } site ? site : throw new NotFoundException("That province was not found.");
}

/// <summary>Names, units, decimals and chart colours for each measured parameter.</summary>
public static class ParameterCatalog
{
    public sealed record Info(MeasuredParameter Parameter, string Label, string Short, string Unit, int Decimals, string Colour, double DefaultMin, double DefaultMax);

    /// <summary>The seven parameters of a reading, in the order the screens show them.</summary>
    public static readonly IReadOnlyList<Info> Readings = new List<Info>
    {
        new(MeasuredParameter.Moisture, "Soil moisture", "Moisture", "%", 1, "#2F80C2", 20.0, 30.0),
        new(MeasuredParameter.Temperature, "Soil temperature", "Temperature", "°C", 1, "#C2612F", 15.0, 28.0),
        new(MeasuredParameter.Ph, "pH level", "pH", "pH", 1, "#61C9A0", 6.0, 7.5),
        new(MeasuredParameter.Conductivity, "Conductivity", "Conductivity", "µS/cm", 0, "#6C55C4", 300, 600),
        new(MeasuredParameter.Nitrogen, "Nitrogen (N)", "Nitrogen", "mg/kg", 0, "#2F80C2", 100, 160),
        new(MeasuredParameter.Phosphorus, "Phosphorus (P)", "Phosphorus", "mg/kg", 0, "#DE9A2E", 40, 80),
        new(MeasuredParameter.Potassium, "Potassium (K)", "Potassium", "mg/kg", 0, "#C2612F", 110, 180),
    };

    public static Info Get(MeasuredParameter parameter) => Readings.First(i => i.Parameter == parameter);
}

/// <summary>Turns values and dates into the exact text the screens show. Times are stored in UTC and shown in South African time (UTC+2).</summary>
public static class Display
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>South African Standard Time is UTC+2 all year (no daylight saving).</summary>
    public static readonly TimeSpan SouthAfrica = TimeSpan.FromHours(2);

    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(SouthAfrica);

    /// <summary>Midnight today in South Africa, as a UTC time.</summary>
    public static DateTime LocalMidnightUtc(DateTime nowUtc) => Local(nowUtc).Date.Subtract(SouthAfrica);

    public static string Num(double value, int decimals) => value.ToString("F" + decimals, Culture);

    public static string Value(MeasuredParameter parameter, double value) => Num(value, ParameterCatalog.Get(parameter).Decimals);

    public static string Time(DateTime utc) => Local(utc).ToString("HH:mm", Culture);

    /// <summary>"Today 06:30", "Yest. 21:15" or "5 Sep 09:40" (used in the alerts table).</summary>
    public static string Stamp(DateTime utc, DateTime nowUtc)
    {
        var t = Local(utc);
        var n = Local(nowUtc);
        return t.Date == n.Date ? $"Today {t:HH:mm}" : t.Date == n.Date.AddDays(-1) ? $"Yest. {t:HH:mm}" : t.ToString("d MMM HH:mm", Culture);
    }

    /// <summary>"Today, 06:30", "Yesterday, 21:15" or "3 September" (used in feeds and user tables).</summary>
    public static string DayLabel(DateTime utc, DateTime nowUtc)
    {
        var t = Local(utc);
        var n = Local(nowUtc);
        return t.Date == n.Date ? $"Today, {t:HH:mm}" : t.Date == n.Date.AddDays(-1) ? $"Yesterday, {t:HH:mm}" : t.ToString("d MMMM", Culture);
    }

    /// <summary>"14:30 today", "Yesterday 14:30" or "3 Sep 14:30".</summary>
    public static string LastReading(DateTime? utc, DateTime nowUtc)
    {
        if (utc is null) return "Never";
        var t = Local(utc.Value);
        var n = Local(nowUtc);
        return t.Date == n.Date ? $"{t:HH:mm} today" : t.Date == n.Date.AddDays(-1) ? $"Yesterday {t:HH:mm}" : t.ToString("d MMM HH:mm", Culture);
    }

    /// <summary>A date that has no time zone (for example the planting date): "18 February 2026".</summary>
    public static string LongDate(DateTime date) => date.ToString("d MMMM yyyy", Culture);

    /// <summary>A moment shown as a South African date: "3 September 2026".</summary>
    public static string LongDateLocal(DateTime utc) => Local(utc).ToString("d MMMM yyyy", Culture);

    public static string Size(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    /// <summary>First letter of the first and last word: "N.M Maphosa" gives "NM".</summary>
    public static string Initials(string name)
    {
        var parts = name.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    public static string RoleLabel(UserRole role) => role == UserRole.Administrator ? "Administrator" : "Field officer";

    public static string StateLabel(SensorState s) => s == SensorState.Online ? "Online" : "Offline";
}

/// <summary>Reads the bands and tells whether a reading is inside them.</summary>
public sealed class Analytics
{
    private readonly IThresholdRepository _thresholds;

    public Analytics(IThresholdRepository thresholds) => _thresholds = thresholds;

    public async Task<Dictionary<MeasuredParameter, Threshold>> BandsForAsync(string siteId) =>
        (await _thresholds.GetAllAsync()).Where(t => t.SiteId == siteId).ToDictionary(t => t.Parameter);

    public async Task<Dictionary<string, Dictionary<MeasuredParameter, Threshold>>> AllBandsAsync() =>
        (await _thresholds.GetAllAsync()).GroupBy(t => t.SiteId).ToDictionary(g => g.Key, g => g.ToDictionary(t => t.Parameter));

    /// <summary>Every parameter of the reading that is outside its band.</summary>
    public static IEnumerable<(MeasuredParameter Parameter, Threshold Band, bool Above)> Breaches(Reading reading, IReadOnlyDictionary<MeasuredParameter, Threshold> bands)
    {
        foreach (var info in ParameterCatalog.Readings)
        {
            if (!bands.TryGetValue(info.Parameter, out var band)) continue;
            var value = reading.ValueOf(info.Parameter);
            if (BreachRule.IsBreach(value, band)) yield return (info.Parameter, band, BreachRule.CrossedMax(value, band));
        }
    }

    public static bool IsInRange(Reading reading, IReadOnlyDictionary<MeasuredParameter, Threshold> bands) => !Breaches(reading, bands).Any();
}

/// <summary>Looks up the live part of many sensors at once (newest status, newest reading), asking the database in parallel.</summary>
public sealed class SensorLookup
{
    private readonly ISensorStatusRepository _statuses;
    private readonly IReadingRepository _readings;

    public SensorLookup(ISensorStatusRepository statuses, IReadingRepository readings)
    {
        _statuses = statuses;
        _readings = readings;
    }

    public async Task<Dictionary<string, SensorStatus?>> StatusesAsync(IEnumerable<Sensor> sensors)
    {
        var list = sensors.ToList();
        var found = await Task.WhenAll(list.Select(s => _statuses.LatestAsync(s.SiteId, s.SensorId)));
        return list.Select((s, i) => (s.SensorId, found[i])).ToDictionary(x => x.SensorId, x => x.Item2);
    }

    public async Task<Dictionary<string, Reading?>> LatestReadingsAsync(IEnumerable<Sensor> sensors)
    {
        var list = sensors.ToList();
        var found = await Task.WhenAll(list.Select(s => _readings.LatestAsync(s.SiteId, s.SensorId)));
        return list.Select((s, i) => (s.SensorId, found[i])).ToDictionary(x => x.SensorId, x => x.Item2);
    }

    /// <summary>All readings of the given sensors between two times, by sensor id.</summary>
    public async Task<Dictionary<string, IReadOnlyList<Reading>>> ReadingsAsync(IEnumerable<Sensor> sensors, DateTime fromUtc, DateTime toUtc)
    {
        var list = sensors.ToList();
        var found = await Task.WhenAll(list.Select(s => _readings.QueryAsync(s.SiteId, s.SensorId, fromUtc, toUtc)));
        return list.Select((s, i) => (s.SensorId, found[i])).ToDictionary(x => x.SensorId, x => x.Item2);
    }
}

/// <summary>Shared maths for the pages that summarise a province.</summary>
public sealed class SiteMetrics
{
    private readonly SensorLookup _lookup;
    private readonly Analytics _analytics;
    private readonly IClock _clock;

    public SiteMetrics(SensorLookup lookup, Analytics analytics, IClock clock)
    {
        _lookup = lookup;
        _analytics = analytics;
        _clock = clock;
    }

    /// <summary>The newest reading of each reporting sensor in the province (sensors left out of averages are skipped).</summary>
    public async Task<List<Reading>> CurrentReadingsAsync(IReadOnlyList<Sensor> siteSensors)
    {
        var now = _clock.UtcNow;
        var statuses = await _lookup.StatusesAsync(siteSensors);
        var counted = siteSensors.Where(s => s.IsActive && s.IncludeInAverage && !OfflineRule.IsOffline(statuses[s.SensorId]?.LastCommunication, now)).ToList();
        var latest = await _lookup.LatestReadingsAsync(counted);
        return latest.Values.Where(r => r is not null).Select(r => r!).ToList();
    }

    /// <summary>The share of readings in the last 24 hours that sit inside every band (0 to 100).</summary>
    public async Task<int> InRangePercentAsync(string siteId, IReadOnlyList<Sensor> siteSensors)
    {
        var now = _clock.UtcNow;
        var bands = await _analytics.BandsForAsync(siteId);
        var recent = (await _lookup.ReadingsAsync(siteSensors, now.AddHours(-24), now)).Values.SelectMany(x => x).ToList();
        return recent.Count == 0 ? 0 : (int)Math.Round(100.0 * recent.Count(r => Analytics.IsInRange(r, bands)) / recent.Count);
    }

    public static (string Label, Tone Tone) StatusFor(int percent) => percent >= 70 ? ("Optimal", Tone.Ok) : ("Attention", Tone.Warn);

    public static double Mean(IEnumerable<Reading> readings, MeasuredParameter p)
    {
        var list = readings.ToList();
        return list.Count == 0 ? 0 : list.Average(r => r.ValueOf(p));
    }
}
