// What this file is for: the numbers and lists behind the Dashboard, Sites and Site detail pages, and the moisture chart data for 24 hours / 7 days / 30 days.
// How it fits in: Service folder. Controllers call these through IDashboardService and ISiteService; every method first limits data to the provinces the person may see.
// Readings are always read as a key range per sensor, never the whole readings branch.
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class DashboardService : IDashboardService
{
    private static readonly string[] ProvinceColours = { "#10795A", "#DE9A2E", "#2F80C2" };

    private readonly DataScope _scope;
    private readonly SensorLookup _lookup;
    private readonly IAlertRepository _alerts;
    private readonly Analytics _analytics;
    private readonly SiteMetrics _metrics;
    private readonly IClock _clock;

    public DashboardService(DataScope scope, SensorLookup lookup, IAlertRepository alerts, Analytics analytics, SiteMetrics metrics, IClock clock)
    {
        _scope = scope;
        _lookup = lookup;
        _alerts = alerts;
        _analytics = analytics;
        _metrics = metrics;
        _clock = clock;
    }

    public async Task<DashboardDto> GetAsync(UserContext ctx)
    {
        var now = _clock.UtcNow;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).Where(s => s.IsActive).ToList();
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var statuses = await _lookup.StatusesAsync(sensors);
        var latest = await _lookup.LatestReadingsAsync(sensors);

        var offline = sensors.Where(s => OfflineRule.IsOffline(statuses[s.SensorId]?.LastCommunication, now)).ToList();
        var reporting = sensors.Count - offline.Count;
        var offlineText = offline.Count == 0 ? "All units reporting"
            : offline.Select(s => s.SiteId).Distinct().Count() == 1
                ? $"{offline.Count} {(offline.Count == 1 ? "unit" : "units")} offline in {siteNames[offline[0].SiteId]}"
                : $"{offline.Count} units offline";

        var today = (await _lookup.ReadingsAsync(sensors, Display.LocalMidnightUtc(now), now)).Values.Sum(l => l.Count);
        var perProvince = sites.Count == 0 ? 0 : today / sites.Count;

        var open = (await _alerts.GetAllAsync(sites.Select(s => s.SiteId))).Where(a => a.Status == AlertStatus.Open).ToList();
        var parts = new List<string>();
        var crit = open.Count(a => a.AlertType != AlertType.Offline && a.Severity == AlertSeverity.Critical);
        var warn = open.Count(a => a.AlertType != AlertType.Offline && a.Severity == AlertSeverity.Warning);
        var off = open.Count(a => a.AlertType == AlertType.Offline);
        if (crit > 0) parts.Add($"{crit} critical");
        if (warn > 0) parts.Add($"{warn} warning");
        if (off > 0) parts.Add($"{off} offline");

        var kpis = new List<KpiDto>
        {
            new("Sensors reporting", $"{reporting} / {sensors.Count}", offlineText, Tone.Ok),
            new("Monitored provinces", sites.Count.ToString(), string.Join(", ", sites.Select(s => s.Name)), Tone.Info),
            new("Readings today", today.ToString("N0", Display.Culture), $"{perProvince:N0} per province since midnight", Tone.Violet),
            new("Open alerts", open.Count.ToString(), parts.Count == 0 ? "Nothing open" : string.Join(", ", parts), Tone.Bad)
        };

        var siteStatuses = new List<SiteStatusDto>();
        foreach (var site in sites)
        {
            var mine = sensors.Where(s => s.SiteId == site.SiteId).ToList();
            var live = mine.Count(s => !OfflineRule.IsOffline(statuses[s.SensorId]?.LastCommunication, now));
            var pct = await _metrics.InRangePercentAsync(site.SiteId, mine);
            var (label, tone) = SiteMetrics.StatusFor(pct);
            siteStatuses.Add(new SiteStatusDto(site.SiteId, site.Name, $"{live} of {mine.Count} sensors", label, tone, pct));
        }

        // newest readings first; a reading that is out of band comes before healthy ones
        var bands = await _analytics.AllBandsAsync();
        var rows = latest.Values.Where(r => r is not null).Select(r => r!)
            .OrderByDescending(r => r.Timestamp)
            .ThenBy(r => bands.TryGetValue(r.SiteId, out var b) && Analytics.IsInRange(r, b) ? 1 : 0)
            .ThenBy(r => r.SensorId, StringComparer.Ordinal).Take(6)
            .Select(r =>
            {
                var (text, tone) = ReadingStatus(r, bands.GetValueOrDefault(r.SiteId) ?? new());
                return new LatestReadingDto(Display.Time(r.Timestamp), siteNames[r.SiteId], r.SensorId, ReadingValues(r), text, tone);
            }).ToList();

        var lastSync = statuses.Values.Where(s => s is not null).Select(s => s!.LastCommunication).DefaultIfEmpty().Max();
        var subtitle = $"{Display.Local(now).ToString("dddd, d MMMM yyyy", Display.Culture)} · last sync {(lastSync == default ? "never" : Display.Time(lastSync))}";
        return new DashboardDto(subtitle, kpis, siteStatuses, rows, $"{today.ToString("N0", Display.Culture)} readings captured today across {sensors.Count} sensors");
    }

    /// <summary>The seven values of a reading as display text, in the column order of the tables.</summary>
    public static IReadOnlyList<string> ReadingValues(Reading r) =>
        ParameterCatalog.Readings.Select(i => Display.Value(i.Parameter, r.ValueOf(i.Parameter))).ToList();

    /// <summary>"In range" or the first parameter that is low or high, with a colour.</summary>
    public static (string Text, Tone Tone) ReadingStatus(Reading r, IReadOnlyDictionary<MeasuredParameter, Threshold> bands)
    {
        var breach = Analytics.Breaches(r, bands).FirstOrDefault();
        if (breach.Band is null) return ("In range", Tone.Ok);
        var value = r.ValueOf(breach.Parameter);
        var limit = breach.Above ? breach.Band.MaxValue : breach.Band.MinValue;
        var tone = SeverityRule.For(value, limit) == AlertSeverity.Critical ? Tone.Bad : Tone.Warn;
        return ($"{ParameterCatalog.Get(breach.Parameter).Short} {(breach.Above ? "high" : "low")}", tone);
    }

    public async Task<MoistureChartDto> GetMoistureChartAsync(UserContext ctx, string? range)
    {
        var clock = _clock.UtcNow;
        // end on the last half-hour slot, so the axis labels read 14:30 and not 14:32
        var now = new DateTime(clock.Year, clock.Month, clock.Day, clock.Hour, clock.Minute < 30 ? 0 : 30, 0, DateTimeKind.Utc);
        var (window, caption, labelFormat) = range switch
        {
            "7d" => (TimeSpan.FromDays(7), "last 7 days", "d MMM"),
            "30d" => (TimeSpan.FromDays(30), "last 30 days", "d MMM"),
            _ => (TimeSpan.FromHours(24), "last 24 hours", "HH:mm")
        };
        const int points = 13;
        var step = window / (points - 1);
        var start = now - window;
        var half = step / 2;

        var sites = await _scope.VisibleSitesAsync(ctx);
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).Where(s => s.IsActive).ToList();
        var readings = (await _lookup.ReadingsAsync(sensors, start - half, now)).Values.SelectMany(x => x).ToList();

        var series = new List<ChartSeriesDto>();
        for (var si = 0; si < sites.Count; si++)
        {
            var site = sites[si];
            var mine = readings.Where(r => r.SiteId == site.SiteId).ToList();
            var values = new double[points];
            double? previous = null;
            for (var i = 0; i < points; i++)
            {
                var centre = start + step * i;
                var bucket = mine.Where(r => r.Timestamp >= centre - half && r.Timestamp < centre + half).ToList();
                if (bucket.Count > 0) previous = bucket.Average(r => r.Moisture);
                values[i] = Math.Round(previous ?? 0, 1);
            }
            // leading gap: copy the first real value backwards so the line starts at a real number
            var first = values.FirstOrDefault(v => v > 0);
            for (var i = 0; i < points && values[i] == 0; i++) values[i] = first;
            series.Add(new ChartSeriesDto(site.SiteId, site.Name, ProvinceColours[si % ProvinceColours.Length], values));
        }

        var labels = Enumerable.Range(0, 8).Select(i => Display.Local(start + (window / 7) * i).ToString(labelFormat, Display.Culture)).ToList();
        return new MoistureChartDto(labels, series, $"Average moisture of each province's sensors over the {caption}");
    }
}

public sealed class SiteService : ISiteService
{
    private readonly DataScope _scope;
    private readonly SiteMetrics _metrics;
    private readonly SensorLookup _lookup;
    private readonly IAlertRepository _alerts;
    private readonly IThresholdRepository _thresholds;
    private readonly IUserRepository _users;
    private readonly Analytics _analytics;
    private readonly IClock _clock;
    private readonly ILocationRepository _locations;

    public SiteService(DataScope scope, SiteMetrics metrics, SensorLookup lookup, IAlertRepository alerts, IThresholdRepository thresholds, IUserRepository users, Analytics analytics, IClock clock, ILocationRepository locations)
    {
        _locations = locations;
        _scope = scope;
        _metrics = metrics;
        _lookup = lookup;
        _alerts = alerts;
        _thresholds = thresholds;
        _users = users;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<SitesPageDto> GetSitesAsync(UserContext ctx)
    {
        var now = _clock.UtcNow;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).ToList();
        var counts = sites.Select(s => sensors.Count(x => x.SiteId == s.SiteId)).Distinct().ToList();
        var crops = sites.Select(s => s.Crop.ToLowerInvariant()).Distinct().ToList();
        var subtitle = $"{sites.Count} {(sites.Count == 1 ? "province" : "provinces")}, " +
                       (counts.Count == 1 ? $"{counts[0]} sensors each" : $"{sensors.Count} sensors") +
                       (crops.Count == 1 ? $", growing {crops[0]}" : "");

        var statuses = await _lookup.StatusesAsync(sensors);
        var midnight = Display.LocalMidnightUtc(now);
        var cards = new List<SiteCardDto>();
        var averages = new List<AverageRowDto>();
        foreach (var site in sites)
        {
            var mine = sensors.Where(s => s.SiteId == site.SiteId).ToList();
            var pct = await _metrics.InRangePercentAsync(site.SiteId, mine);
            var (label, tone) = SiteMetrics.StatusFor(pct);
            var today = (await _lookup.ReadingsAsync(mine, midnight, now)).Values.Sum(l => l.Count);
            cards.Add(new SiteCardDto(site.SiteId, site.Name, Coordinates(site), label, tone, $"{site.Crop} · planted {Display.LongDate(site.PlantingDate)}", mine.Count, today, pct));

            var current = await _metrics.CurrentReadingsAsync(mine);
            var bands = await _analytics.BandsForAsync(site.SiteId);
            var breaching = current.Count(r => !Analytics.IsInRange(r, bands));
            averages.Add(new AverageRowDto(site.Name,
                ParameterCatalog.Readings.Select(i => Display.Value(i.Parameter, SiteMetrics.Mean(current, i.Parameter))).ToList(),
                breaching == 0 ? "All in range" : $"{breaching} {(breaching == 1 ? "breach" : "breaches")}", breaching == 0 ? Tone.Ok : Tone.Bad));
        }

        var newest = statuses.Values.Where(s => s is not null).Select(s => s!.LastCommunication).DefaultIfEmpty().Max();
        var caption = $"Mean across the reporting sensors in each province, taken from the {(newest == default ? "latest" : Display.Time(newest))} reading";
        return new SitesPageDto(subtitle, caption, cards, averages, await BuildFeedAsync(sites, now));
    }

    public async Task<SiteDetailDto> GetDetailAsync(UserContext ctx, string siteId)
    {
        var site = await _scope.RequireSiteAsync(ctx, siteId);
        var now = _clock.UtcNow;
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).Where(s => s.SiteId == siteId).ToList();
        var current = await _metrics.CurrentReadingsAsync(sensors);
        var bands = await _analytics.BandsForAsync(siteId);
        var statuses = await _lookup.StatusesAsync(sensors);

        var cards = new List<ParamCardDto>();
        var outside = 0;
        foreach (var info in ParameterCatalog.Readings)
        {
            var value = SiteMetrics.Mean(current, info.Parameter);
            if (!bands.TryGetValue(info.Parameter, out var band)) continue;
            var breach = BreachRule.IsBreach(value, band);
            if (breach) outside++;
            var label = !breach ? "Optimal" : value > band.MaxValue ? "High" : "Low";
            // the marker sits on a bar where the target band covers 18% to 76% of the width
            var position = (int)Math.Round(Math.Clamp(18 + 58 * (value - band.MinValue) / (band.MaxValue - band.MinValue), 2, 96));
            cards.Add(new ParamCardDto(info.Label, Display.Value(info.Parameter, value), info.Unit, label, breach ? Tone.Bad : Tone.Ok, info.Colour, position));
        }

        var newest = statuses.Values.Where(s => s is not null).Select(s => s!.LastCommunication).DefaultIfEmpty().Max();
        var updated = newest == default ? "no readings yet" : Ago(now - newest);
        var offline = sensors.Count(s => OfflineRule.IsOffline(statuses[s.SensorId]?.LastCommunication, now));
        return new SiteDetailDto(site.SiteId, site.Name, $"{Coordinates(site)} · {site.Crop.ToLowerInvariant()} · {sensors.Count} sensors", cards,
            outside == 0 ? $"All {cards.Count} parameters inside their target bands" : $"{outside} of {cards.Count} parameters outside their target bands",
            outside == 0 ? Tone.Ok : Tone.Bad, "updated " + updated,
            sensors.Select(s => SensorRowDto.From(s, site.Name, statuses[s.SensorId], now)).ToList(),
            $"{sensors.Count} units, " + (offline == 0 ? "all reporting" : $"{offline} offline"));
    }

    private static string Coordinates(Site s) =>
        $"{s.Latitude.ToString("F4", Display.Culture)}, {s.Longitude.ToString("F4", Display.Culture)}";

    private static string Ago(TimeSpan span) =>
        span.TotalMinutes < 1 ? "just now" :
        span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} {((int)span.TotalMinutes == 1 ? "minute" : "minutes")} ago" :
        span.TotalHours < 24 ? $"{(int)span.TotalHours} {((int)span.TotalHours == 1 ? "hour" : "hours")} ago" :
        $"{(int)span.TotalDays} days ago";

    /// <summary>The five newest events (alerts raised or resolved, threshold edits) in the person's provinces.</summary>
    private async Task<List<FeedItemDto>> BuildFeedAsync(IReadOnlyList<Site> sites, DateTime now)
    {
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var userNames = (await _users.GetAllAsync()).ToDictionary(u => u.UserId, u => u.Name);
        var events = new List<(DateTime When, FeedItemDto Item)>();

        foreach (var a in await _alerts.GetAllAsync(sites.Select(s => s.SiteId)))
        {
            var province = siteNames.GetValueOrDefault(a.SiteId, a.SiteId);
            if (a.AlertType == AlertType.Offline)
            {
                events.Add((a.RaisedAt, new FeedItemDto(Display.DayLabel(a.RaisedAt, now), province, $"{a.SensorId} stopped reporting", Tone.Off)));
                if (a.Resolved is { } back) events.Add((back.ActedAt, new FeedItemDto(Display.DayLabel(back.ActedAt, now), province, $"{a.SensorId} reconnected", Tone.Info)));
            }
            else
            {
                var p = ParameterCatalog.Get(a.Parameter ?? MeasuredParameter.Battery).Short;
                var word = a.AlertType == AlertType.Battery ? "battery fell below 25%" : a.ThresholdIsMax ? "rose above the configured maximum" : "fell below the configured minimum";
                var text = a.AlertType == AlertType.Battery ? $"{a.SensorId} {word}" : $"{p} {word} on {a.SensorId}";
                events.Add((a.RaisedAt, new FeedItemDto(Display.DayLabel(a.RaisedAt, now), province, text, a.Severity == AlertSeverity.Critical ? Tone.Bad : Tone.Warn)));
                if (a.Resolved is { } fixedAt) events.Add((fixedAt.ActedAt, new FeedItemDto(Display.DayLabel(fixedAt.ActedAt, now), province, $"{p} returned to range on {a.SensorId}", Tone.Ok)));
            }
        }

        foreach (var group in (await _thresholds.GetAllAsync()).Where(t => siteNames.ContainsKey(t.SiteId)).GroupBy(t => t.SiteId))
        {
            var last = group.OrderByDescending(t => t.UpdatedAt).First();
            events.Add((last.UpdatedAt, new FeedItemDto(Display.DayLabel(last.UpdatedAt, now), siteNames[group.Key],
                $"Threshold bands updated by {userNames.GetValueOrDefault(last.UpdatedBy, "an administrator")}", Tone.Off)));
        }

        return events.Where(e => e.When <= now).OrderByDescending(e => e.When).Take(5).Select(e => e.Item).ToList();
    }

    /// <summary>
    /// Adds a site (administrators only). Its id is the two letter prefix of the sensor codes (FS for FS-01), so it must match what the sensor gateway writes.
    /// The site starts with the standard pigeon pea bands so alerts work at once; the administrator reviews them on the Thresholds page.
    /// </summary>
    public async Task<SiteSaveResult> AddAsync(UserContext ctx, SiteForm form)
    {
        ctx.RequireAdmin();
        var errors = new Dictionary<string, string>();
        var id = form.SiteId?.Trim().ToUpperInvariant() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Z]{2}$")) errors["SiteId"] = "Use two capital letters, for example FS. Sensor codes at this site will start with them.";
        else if (await _locations.GetAsync(id) is not null) errors["SiteId"] = "A site with this code already exists.";
        var name = form.Name?.Trim() ?? "";
        if (name.Length is 0 or > 60) errors["Name"] = "Enter a name (up to 60 characters).";
        var crop = form.Crop?.Trim() ?? "";
        if (crop.Length is 0 or > 40) errors["Crop"] = "Enter the crop (up to 40 characters).";
        if (!double.TryParse(form.Latitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) || lat is < -90 or > 90)
            errors["Latitude"] = "Latitude is a number from -90 to 90, for example -28.4541.";
        if (!double.TryParse(form.Longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon) || lon is < -180 or > 180)
            errors["Longitude"] = "Longitude is a number from -180 to 180, for example 26.7968.";
        if (form.PlantingDate is null) errors["PlantingDate"] = "Enter the planting date.";
        var description = form.Description?.Trim() ?? "";
        if (description.Length > 200) errors["Description"] = "Keep the description under 200 characters.";
        if (errors.Count > 0) return new SiteSaveResult(false, errors);

        var now = _clock.UtcNow;
        await _locations.SaveAsync(new Site
        {
            SiteId = id, Name = name, Description = description, Crop = crop, Latitude = lat, Longitude = lon,
            PlantingDate = form.PlantingDate!.Value.Date, CreatedAt = now
        });
        await _thresholds.SaveAsync(ParameterCatalog.Readings.Select(i => new Threshold
        {
            SiteId = id, Parameter = i.Parameter, MinValue = i.DefaultMin, MaxValue = i.DefaultMax,
            Basis = "Standard pigeon pea bands, set when the site was added", UpdatedBy = ctx.UserId, UpdatedAt = now
        }));
        return new SiteSaveResult(true, errors);
    }
}
