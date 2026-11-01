// What this file is for: the figures of each sensor for each day (count, lowest, average and highest values, share in range, gaps and offline time),
// and the totals of a whole period built from them. The Reports page and the monthly summary report use it.
// How it fits in: Service folder. The gateway writes a reading about every 6 seconds (about 14,400 a day per sensor), so a month of raw readings is far
// too much to read for every page. A finished day never changes, so its figures are worked out ONCE and saved at daily_stats/{sensor}/{day};
// after that a month is about 30 small records. Today keeps changing, so it is always worked out from its readings (remembered for one minute).
using System.Collections.Concurrent;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class DailyFigures
{
    /// <summary>How long today's figures are remembered before they are worked out again.</summary>
    private static readonly TimeSpan TodayKeep = TimeSpan.FromMinutes(1);

    private readonly IDailyStatsRepository _stats;
    private readonly IReadingRepository _readings;
    private readonly Analytics _analytics;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, (DateTime Until, DailyStats Stats)> _today = new();

    public DailyFigures(IDailyStatsRepository stats, IReadingRepository readings, Analytics analytics, IClock clock)
    {
        _stats = stats;
        _readings = readings;
        _analytics = analytics;
        _clock = clock;
    }

    /// <summary>
    /// The figures of each sensor for every South African day from firstDay to lastDay (dates only), by sensor id, oldest day first.
    /// Days after today are left out. Days before the sensor was registered are included too (the gateway may have sent readings already).
    /// </summary>
    public async Task<Dictionary<string, IReadOnlyList<DailyStats>>> ForSensorsAsync(IEnumerable<Sensor> sensors, DateTime firstDay, DateTime lastDay)
    {
        var list = sensors.ToList();
        var bands = await _analytics.AllBandsAsync();
        var found = await Task.WhenAll(list.Select(s => ForSensorAsync(s, firstDay.Date, lastDay.Date, bands.GetValueOrDefault(s.SiteId) ?? new())));
        return list.Select((s, i) => (s.SensorId, found[i])).ToDictionary(x => x.SensorId, x => x.Item2);
    }

    private async Task<IReadOnlyList<DailyStats>> ForSensorAsync(Sensor sensor, DateTime firstDay, DateTime lastDay, Dictionary<MeasuredParameter, Threshold> bands)
    {
        var today = Display.Local(_clock.UtcNow).Date;
        if (lastDay > today) lastDay = today;
        if (firstDay > lastDay) return new List<DailyStats>();

        // finished days: the saved ones, and any day not saved yet is worked out now and saved for next time (four at a time)
        var days = new List<DailyStats>();
        if (firstDay < today)
        {
            var lastFinished = lastDay < today ? lastDay : today.AddDays(-1);
            var saved = (await _stats.GetRangeAsync(sensor.SensorId, firstDay, lastFinished)).ToDictionary(d => d.Day);
            var missing = new List<DateTime>();
            for (var d = firstDay; d <= lastFinished; d = d.AddDays(1))
                if (saved.TryGetValue(d, out var s)) days.Add(s);
                else missing.Add(d);

            foreach (var batch in missing.Chunk(4))
            {
                var made = await Task.WhenAll(batch.Select(async d =>
                {
                    var stats = await WorkOutAsync(sensor, d, bands, DayEndUtc(d));
                    await _stats.SaveAsync(stats);
                    return stats;
                }));
                days.AddRange(made);
            }
        }

        // today: worked out from its readings, remembered for a minute so a page that refreshes does not read them again every time
        if (lastDay == today)
        {
            if (_today.TryGetValue(sensor.SensorId, out var hit) && hit.Until > _clock.UtcNow && hit.Stats.Day == today) days.Add(hit.Stats);
            else
            {
                var stats = await WorkOutAsync(sensor, today, bands, _clock.UtcNow);
                _today[sensor.SensorId] = (_clock.UtcNow + TodayKeep, stats);
                days.Add(stats);
            }
        }
        return days.OrderBy(d => d.Day).ToList();
    }

    /// <summary>Reads one day of one sensor and works out its figures, up to endUtc (the end of the day, or now for today).</summary>
    private async Task<DailyStats> WorkOutAsync(Sensor sensor, DateTime day, Dictionary<MeasuredParameter, Threshold> bands, DateTime endUtc)
    {
        var startUtc = DayStartUtc(day);
        var rows = (await _readings.QueryAsync(sensor.SiteId, sensor.SensorId, startUtc, endUtc.AddMilliseconds(-1))).OrderBy(r => r.Timestamp).ToList();
        return Summarise(sensor.SensorId, day, rows, bands, startUtc, endUtc);
    }

    /// <summary>
    /// The figures of one day from its readings. Every row counts as the sensor talking (for gaps and offline time); rows of zeros are left out of the values.
    /// </summary>
    public static DailyStats Summarise(string sensorId, DateTime day, IReadOnlyList<Reading> rows, IReadOnlyDictionary<MeasuredParameter, Threshold> bands,
        DateTime startUtc, DateTime endUtc)
    {
        var stats = new DailyStats { SensorId = sensorId, Day = day };
        foreach (var r in rows.Where(r => !ReadingRule.IsEmpty(r)))
        {
            stats.Readings++;
            if (Analytics.IsInRange(r, bands)) stats.InRange++;
            foreach (var info in ParameterCatalog.Readings)
            {
                var v = r.ValueOf(info.Parameter);
                stats.Values[info.Parameter] = stats.Values.TryGetValue(info.Parameter, out var x)
                    ? (Math.Min(x.Min, v), x.Sum + v, Math.Max(x.Max, v))
                    : (v, v, v);
            }
        }

        if (rows.Count == 0)
        {
            stats.Offline = endUtc - startUtc; // silent all day
            return stats;
        }
        stats.FirstAt = rows[0].Timestamp;
        stats.LastAt = rows[^1].Timestamp;

        // a silence of 1 minute or more counts as offline, the same rule as the alerts; the start and end of the day count too
        void Silence(TimeSpan gap)
        {
            if (gap >= OfflineRule.SilenceLimit) stats.Offline += gap;
        }
        Silence(rows[0].Timestamp - startUtc);
        for (var i = 1; i < rows.Count; i++)
        {
            var gap = rows[i].Timestamp - rows[i - 1].Timestamp;
            if (gap > stats.LongestGap) stats.LongestGap = gap;
            Silence(gap);
        }
        Silence(endUtc - rows[^1].Timestamp);
        return stats;
    }

    /// <summary>The start of a South African day, in UTC.</summary>
    public static DateTime DayStartUtc(DateTime day) => DateTime.SpecifyKind(day.Date - Display.SouthAfrica, DateTimeKind.Utc);

    /// <summary>The end of a South African day (the start of the next one), in UTC.</summary>
    public static DateTime DayEndUtc(DateTime day) => DayStartUtc(day.AddDays(1));
}

/// <summary>The totals of many days (of one or more sensors): what the Reports page and the monthly summary show for a province or a day.</summary>
public sealed class PeriodTotals
{
    public int Readings { get; private set; }
    public int InRange { get; private set; }
    private readonly Dictionary<MeasuredParameter, (double Min, double Sum, double Max)> _values = new();
    private TimeSpan _covered;  // the time the sensors were expected to report
    private TimeSpan _offline;  // the part of it they were silent for 1 minute or more

    /// <summary>
    /// Adds the days of one sensor. Today counts up to nowUtc; finished days count in full. For the uptime, a day before the sensor was
    /// installed only counts when it has readings (the sensor was not expected to report yet).
    /// </summary>
    public void Add(IEnumerable<DailyStats> days, DateTime nowUtc, DateTime installed = default)
    {
        foreach (var d in days)
        {
            Readings += d.Readings;
            InRange += d.InRange;
            foreach (var (p, v) in d.Values)
                _values[p] = _values.TryGetValue(p, out var x) ? (Math.Min(x.Min, v.Min), x.Sum + v.Sum, Math.Max(x.Max, v.Max)) : v;
            if (d.Day < installed.Date && d.FirstAt is null) continue;
            var end = DailyFigures.DayEndUtc(d.Day);
            _covered += (end < nowUtc ? end : nowUtc) - DailyFigures.DayStartUtc(d.Day);
            _offline += d.Offline;
        }
    }

    public double? Average(MeasuredParameter p) => Readings > 0 && _values.TryGetValue(p, out var v) ? v.Sum / Readings : null;
    public double? Min(MeasuredParameter p) => _values.TryGetValue(p, out var v) ? v.Min : null;
    public double? Max(MeasuredParameter p) => _values.TryGetValue(p, out var v) ? v.Max : null;

    /// <summary>The share of readings with every value inside its band, 0 to 100.</summary>
    public int InRangePercent => Readings == 0 ? 0 : (int)Math.Round(100.0 * InRange / Readings);

    /// <summary>The share of the time the sensors were not offline, 0 to 100 (null when nothing was expected yet).</summary>
    public double? UptimePercent => _covered <= TimeSpan.Zero ? null : Math.Clamp(100.0 * (1 - _offline / _covered), 0, 100);

    /// <summary>The longest silence of any one sensor, counting the silence from its last reading up to toUtc.</summary>
    public static TimeSpan LongestGap(IEnumerable<IReadOnlyList<DailyStats>> sensors, DateTime toUtc)
    {
        var longest = TimeSpan.Zero;
        foreach (var days in sensors)
        {
            DateTime? lastSeen = null;
            foreach (var d in days.Where(d => d.FirstAt is not null))
            {
                if (d.LongestGap > longest) longest = d.LongestGap;
                if (lastSeen is { } seen && d.FirstAt!.Value - seen > longest) longest = d.FirstAt.Value - seen;
                lastSeen = d.LastAt;
            }
            if (lastSeen is { } last && toUtc - last > longest) longest = toUtc - last;
        }
        return longest;
    }
}
