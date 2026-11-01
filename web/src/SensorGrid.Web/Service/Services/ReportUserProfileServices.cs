// What this file is for: the Reports page and report downloads, user account management (administrators only), and the Profile page.
// How it fits in: Service folder. Reports only ever contain data from the provinces of the person asking. Only the description of a report is
// stored (reports/{id}); the PDF or CSV is built from the data at the moment it is downloaded, so no file is kept anywhere.
// The month's figures come from DailyFigures (one small record per sensor per day), never from a whole month of raw readings.
using System.Globalization;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class ReportService : IReportService
{
    /// <summary>A raw readings export covers at most this many days (a reading every 6 seconds is about 14,400 rows a day per sensor).</summary>
    public const int RawReadingsDays = 7;

    /// <summary>How far back a raw readings export may start.</summary>
    private const int RawReadingsLookBackDays = 90;

    private readonly DataScope _scope;
    private readonly SensorLookup _lookup;
    private readonly DailyFigures _daily;
    private readonly ILocationRepository _locations;
    private readonly ISensorRepository _sensors;
    private readonly IAlertRepository _alerts;
    private readonly IReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly Analytics _analytics;
    private readonly ICsvWriter _csv;
    private readonly IPdfWriter _pdf;
    private readonly IClock _clock;

    public ReportService(DataScope scope, SensorLookup lookup, DailyFigures daily, ILocationRepository locations, ISensorRepository sensors, IAlertRepository alerts,
        IReportRepository reports, IUserRepository users, Analytics analytics, ICsvWriter csv, IPdfWriter pdf, IClock clock)
    {
        _scope = scope;
        _lookup = lookup;
        _daily = daily;
        _locations = locations;
        _sensors = sensors;
        _alerts = alerts;
        _reports = reports;
        _users = users;
        _analytics = analytics;
        _csv = csv;
        _pdf = pdf;
        _clock = clock;
    }

    /// <summary>The months that can be reported on: the current month and the two before it, newest first.</summary>
    public IReadOnlyList<(string Value, string Label)> Periods()
    {
        var now = Display.Local(_clock.UtcNow);
        var list = new List<(string, string)>();
        for (var m = new DateTime(now.Year, now.Month, 1); list.Count < 3; m = m.AddMonths(-1))
            list.Add((m.ToString("yyyy-MM"), m.ToString("MMMM yyyy", Display.Culture)));
        return list;
    }

    public async Task<ReportsPageDto> GetPageAsync(UserContext ctx, string? period)
    {
        var periods = Periods();
        if (period is null || periods.All(p => p.Value != period)) period = periods[0].Value;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var (first, last) = Month(period);
        var data = await AnalyseAsync(sites, first, last);
        var visibleIds = sites.Select(s => s.SiteId).ToHashSet();
        var userNames = (await _users.GetAllAsync()).ToDictionary(u => u.UserId, u => u.Name);

        var generated = (await _reports.GetAllAsync())
            .Where(r => ctx.IsAdmin || r.GeneratedBy == ctx.UserId || (r.SiteIds.Count > 0 && r.SiteIds.All(visibleIds.Contains)))
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new GeneratedReportDto(r.ReportId, r.Name, $"{TreeText(r.Format)} · {PeriodText(r.PeriodStart, r.PeriodEnd)}",
                $"Generated {Display.Local(r.GeneratedAt).ToString("d MMMM", Display.Culture)} by {userNames.GetValueOrDefault(r.GeneratedBy, "unknown")}")).ToList();

        var words = new[] { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        var subtitle = $"Nutrient and moisture analysis across {(sites.Count < words.Length ? words[sites.Count] : sites.Count.ToString())} {(sites.Count == 1 ? "site" : "sites")}";
        var today = Display.Local(_clock.UtcNow).Date;
        return new ReportsPageDto(subtitle, period, periods.First(p => p.Value == period).Label, periods, data.Nutrients, data.InRange, data.Summary, generated,
            today.AddDays(1 - RawReadingsDays), today.AddDays(-RawReadingsLookBackDays), today);
    }

    /// <summary>
    /// Records a report (what it covers and for which provinces). Nothing is built yet: the file is made when it is downloaded.
    /// The monthly summary and the breach log cover a month; raw readings (CSV only) cover 7 days from the chosen first day.
    /// </summary>
    public async Task<GenerateReportResult> GenerateAsync(UserContext ctx, ReportKind kind, ReportFormat format, string? period, DateTime? from)
    {
        DateTime first, last;
        string label;
        if (kind == ReportKind.RawReadings)
        {
            if (format == ReportFormat.Pdf) return new GenerateReportResult(false, "Raw readings can only be exported as CSV.", null, "format");
            var today = Display.Local(_clock.UtcNow).Date;
            if (from is null) return new GenerateReportResult(false, "Choose the first day of the raw readings.", null, "from");
            first = from.Value.Date;
            if (first > today) return new GenerateReportResult(false, "The first day cannot be in the future.", null, "from");
            if (first < today.AddDays(-RawReadingsLookBackDays)) return new GenerateReportResult(false, $"The first day can be at most {RawReadingsLookBackDays} days ago.", null, "from");
            last = first.AddDays(RawReadingsDays - 1);
            label = PeriodText(first, last);
        }
        else
        {
            if (period is null || Periods().All(p => p.Value != period)) return new GenerateReportResult(false, "Choose a month.", null, "period");
            (first, last) = Month(period);
            label = Periods().First(p => p.Value == period).Label;
        }

        var title = kind switch { ReportKind.MonthlySummary => "Monthly summary", ReportKind.RawReadings => "Raw readings", _ => "Threshold breach log" };
        var id = PushIds.At(_clock.UtcNow);
        await _reports.AddAsync(new Report
        {
            ReportId = id, GeneratedBy = ctx.UserId, Name = $"{title} — {label}", Kind = kind, Format = format, PeriodStart = first, PeriodEnd = last,
            GeneratedAt = _clock.UtcNow, SiteIds = (await _scope.VisibleSitesAsync(ctx)).Select(s => s.SiteId).ToList()
        });
        return new GenerateReportResult(true, null, id);
    }

    /// <summary>
    /// Builds the report file now, from the data of its provinces. Only an administrator, the person who made it, or someone who can open
    /// all its provinces may download it.
    /// </summary>
    public async Task<ReportFile> DownloadAsync(UserContext ctx, string reportId)
    {
        var report = await _reports.GetAsync(reportId) ?? throw new NotFoundException("That report was not found.");
        if (!ctx.IsAdmin && report.GeneratedBy != ctx.UserId && !report.SiteIds.All(ctx.CanSee)) throw new NotFoundException("That report was not found.");
        var content = await BuildAsync(report);
        var ext = report.Format == ReportFormat.Csv ? "csv" : "pdf";
        return new ReportFile(content, $"{report.Name.Replace(" — ", "-").Replace(' ', '-')}.{ext}", report.Format == ReportFormat.Csv ? "text/csv" : "application/pdf");
    }

    private static string TreeText(ReportFormat f) => f == ReportFormat.Pdf ? "PDF" : "CSV";

    /// <summary>"1 to 31 October 2026", or "28 October to 3 November 2026".</summary>
    private static string PeriodText(DateTime first, DateTime last) =>
        first.Month == last.Month && first.Year == last.Year
            ? $"{first.Day} to {last.ToString("d MMMM yyyy", Display.Culture)}"
            : $"{first.ToString("d MMMM", Display.Culture)} to {last.ToString("d MMMM yyyy", Display.Culture)}";

    /// <summary>The first and last day of a month written as yyyy-MM.</summary>
    private static (DateTime First, DateTime Last) Month(string period)
    {
        var start = DateTime.ParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    private async Task<byte[]> BuildAsync(Report report)
    {
        var sites = (await _locations.GetAllAsync()).Where(s => report.SiteIds.Contains(s.SiteId)).ToList();
        var sensors = (await _sensors.GetAllAsync()).Where(s => report.SiteIds.Contains(s.SiteId)).ToList();
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var fromUtc = DailyFigures.DayStartUtc(report.PeriodStart);
        var toUtc = DailyFigures.DayEndUtc(report.PeriodEnd);

        switch (report.Kind)
        {
            case ReportKind.RawReadings:
            {
                // reports made before the 7-day limit may cover a whole month: they give its last 7 days
                var first = report.PeriodEnd.AddDays(1 - RawReadingsDays) > report.PeriodStart ? report.PeriodEnd.AddDays(1 - RawReadingsDays) : report.PeriodStart;
                var header = new List<string> { "Timestamp", "Province", "Sensor" };
                header.AddRange(ParameterCatalog.Readings.Select(i => $"{i.Short} ({i.Unit})"));
                var all = (await _lookup.ReadingsAsync(sensors, DailyFigures.DayStartUtc(first), toUtc.AddMilliseconds(-1))).Values.SelectMany(x => x)
                    .OrderBy(r => r.Timestamp).ThenBy(r => r.SensorId, StringComparer.Ordinal);
                var rows = all.Select(r => new[] { Display.Local(r.Timestamp).ToString("yyyy-MM-dd HH:mm:ss"), siteNames[r.SiteId], r.SensorId }
                    .Concat(ParameterCatalog.Readings.Select(i => r.ValueOf(i.Parameter).ToString(CultureInfo.InvariantCulture))));
                return _csv.Write(header, rows);
            }

            case ReportKind.BreachLog:
            {
                var columns = new List<(string, int)> { ("Raised", 90), ("Province", 90), ("Sensor", 60), ("Type", 60), ("Severity", 60), ("Parameter", 90), ("Reading", 70), ("Limit", 80), ("Status", 80) };
                var rows = (await _alerts.GetAllAsync(report.SiteIds)).Where(a => a.RaisedAt >= fromUtc && a.RaisedAt < toUtc).OrderBy(a => a.RaisedAt)
                    .Select(a => AlertRow(a, siteNames)).ToList();
                if (report.Format == ReportFormat.Csv) return _csv.Write(columns.Select(c => c.Item1), rows);
                var blocks = new List<PdfBlock> { new PdfText($"{PeriodText(report.PeriodStart, report.PeriodEnd)} · {string.Join(", ", sites.Select(s => s.Name))} · {rows.Count} alerts") };
                blocks.Add(rows.Count == 0 ? new PdfText("No alerts were raised in this period.") : new PdfTable(columns, rows));
                return _pdf.Write(report.Name, blocks);
            }

            default:
                return await MonthlySummaryAsync(report, sites, sensors, fromUtc, toUtc);
        }
    }

    /// <summary>One row of the breach log (also used for the alerts of the monthly summary).</summary>
    private static List<string> AlertRow(Alert a, IReadOnlyDictionary<string, string> siteNames) => new()
    {
        Display.Local(a.RaisedAt).ToString("yyyy-MM-dd HH:mm"), siteNames.GetValueOrDefault(a.SiteId, a.SiteId), a.SensorId, a.AlertType.ToString(),
        a.AlertType == AlertType.Offline ? "—" : a.Severity.ToString(), a.Parameter is { } p ? ParameterCatalog.Get(p).Short : "—",
        a.ReadingValue?.ToString(CultureInfo.InvariantCulture) ?? "—",
        a.ThresholdValue is { } t ? (a.ThresholdIsMax ? "max " : "min ") + t.ToString(CultureInfo.InvariantCulture) : "—", a.Status.ToString()
    };

    /// <summary>
    /// The monthly summary. PDF: for each province its bands, the month's figures, one row per day (lowest / average / highest) and its alerts.
    /// CSV: the same day rows for every province in one table.
    /// </summary>
    private async Task<byte[]> MonthlySummaryAsync(Report report, IReadOnlyList<Site> sites, IReadOnlyList<Sensor> sensors, DateTime fromUtc, DateTime toUtc)
    {
        var now = _clock.UtcNow;
        var days = await _daily.ForSensorsAsync(sensors.Where(s => s.IsActive), report.PeriodStart, report.PeriodEnd);
        var bands = await _analytics.AllBandsAsync();
        var alerts = (await _alerts.GetAllAsync(report.SiteIds)).Where(a => a.RaisedAt >= fromUtc && a.RaisedAt < toUtc).OrderBy(a => a.RaisedAt).ToList();
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);

        string Mma(PeriodTotals t, MeasuredParameter p) => t.Readings == 0 ? "—"
            : $"{Display.Value(p, t.Min(p)!.Value)} / {Display.Value(p, t.Average(p)!.Value)} / {Display.Value(p, t.Max(p)!.Value)}";
        string Avg(PeriodTotals t, MeasuredParameter p) => t.Average(p) is { } v ? Display.Value(p, v) : "—";

        // one row per day of a province: the totals of all its sensors for that day. Silent days before the first sensor was installed are left out.
        List<List<string>> DayRows(Site site, bool withProvince)
        {
            var siteSensors = sensors.Where(s => s.SiteId == site.SiteId && days.ContainsKey(s.SensorId)).ToList();
            var installed = siteSensors.Count == 0 ? DateTime.MaxValue : siteSensors.Min(s => s.InstalledDate.Date);
            var siteDays = siteSensors.SelectMany(s => days[s.SensorId]).GroupBy(d => d.Day)
                .Where(g => g.Key >= installed || g.Any(d => d.FirstAt is not null)).OrderBy(g => g.Key);
            return siteDays.Select(g =>
            {
                var t = new PeriodTotals();
                t.Add(g, now);
                var dayAlerts = alerts.Count(a => a.SiteId == site.SiteId && Display.Local(a.RaisedAt).Date == g.Key);
                var row = new List<string>();
                if (withProvince) row.Add(site.Name);
                row.AddRange(new[]
                {
                    g.Key.ToString(withProvince ? "yyyy-MM-dd" : "ddd d MMM", Display.Culture), t.Readings.ToString("N0", Display.Culture), t.Readings == 0 ? "—" : $"{t.InRangePercent}%",
                    Mma(t, MeasuredParameter.Moisture), Mma(t, MeasuredParameter.Temperature), Mma(t, MeasuredParameter.Ph), Avg(t, MeasuredParameter.Conductivity),
                    Avg(t, MeasuredParameter.Nitrogen), Avg(t, MeasuredParameter.Phosphorus), Avg(t, MeasuredParameter.Potassium), dayAlerts.ToString()
                });
                return row;
            }).ToList();
        }

        var dayColumns = new List<(string, int)>
        {
            ("Day", 70), ("Readings", 55), ("In range", 50), ("Moisture % (low/avg/high)", 120), ("Temp °C (low/avg/high)", 125),
            ("pH (low/avg/high)", 95), ("EC µS/cm", 55), ("N mg/kg", 48), ("P mg/kg", 48), ("K mg/kg", 48), ("Alerts", 48)
        };

        if (report.Format == ReportFormat.Csv)
        {
            var header = new List<string> { "Province" };
            header.AddRange(dayColumns.Select(c => c.Item1));
            return _csv.Write(header, sites.SelectMany(s => DayRows(s, true)));
        }

        var blocks = new List<PdfBlock>
        {
            new PdfText($"{PeriodText(report.PeriodStart, report.PeriodEnd)}. Each day shows the lowest, average and highest value of all readings that day " +
                        "(rows of zeros left out). In range = readings with every value inside its target band. Times are South African time.")
        };
        foreach (var site in sites)
        {
            var siteSensors = sensors.Where(s => s.SiteId == site.SiteId && s.IsActive).ToList();
            var month = new PeriodTotals();
            foreach (var s in siteSensors.Where(s => days.ContainsKey(s.SensorId))) month.Add(days[s.SensorId], now, s.InstalledDate);
            var siteAlerts = alerts.Where(a => a.SiteId == site.SiteId).ToList();
            var periodEnd = toUtc < now ? toUtc : now;
            var gap = PeriodTotals.LongestGap(siteSensors.Where(s => days.ContainsKey(s.SensorId)).Select(s => days[s.SensorId]), periodEnd);
            var siteBands = bands.GetValueOrDefault(site.SiteId) ?? new();

            blocks.Add(new PdfHeading($"{site.Name} ({site.SiteId})"));
            blocks.Add(new PdfText($"Sensors: {(siteSensors.Count == 0 ? "none" : string.Join(", ", siteSensors.Select(s => s.SensorId)))}. Target bands: " +
                string.Join(", ", ParameterCatalog.Readings.Where(i => siteBands.ContainsKey(i.Parameter))
                    .Select(i => $"{i.Short} {Display.Value(i.Parameter, siteBands[i.Parameter].MinValue)} to {Display.Value(i.Parameter, siteBands[i.Parameter].MaxValue)} {i.Unit}")) + "."));
            blocks.Add(new PdfText(month.Readings == 0
                ? "No readings in this period."
                : $"Month: {month.Readings:N0} readings, {month.InRangePercent}% in range. Averages: moisture {Avg(month, MeasuredParameter.Moisture)} %, " +
                  $"temperature {Avg(month, MeasuredParameter.Temperature)} °C, pH {Avg(month, MeasuredParameter.Ph)}, conductivity {Avg(month, MeasuredParameter.Conductivity)} µS/cm, " +
                  $"N {Avg(month, MeasuredParameter.Nitrogen)}, P {Avg(month, MeasuredParameter.Phosphorus)}, K {Avg(month, MeasuredParameter.Potassium)} mg/kg. " +
                  $"Breach alerts: {siteAlerts.Count(a => a.AlertType != AlertType.Offline)}. Offline alerts: {siteAlerts.Count(a => a.AlertType == AlertType.Offline)}. " +
                  $"Longest silence: {GapText(gap)}. Uptime: {UptimeText(month)}."));
            var rows = DayRows(site, false);
            if (rows.Count > 0) blocks.Add(new PdfTable(dayColumns, rows));
            if (siteAlerts.Count > 0)
                blocks.Add(new PdfTable(new List<(string, int)> { ("Raised", 90), ("Province", 90), ("Sensor", 60), ("Type", 60), ("Severity", 60), ("Parameter", 90), ("Reading", 70), ("Limit", 80), ("Status", 80) },
                    siteAlerts.Select(a => AlertRow(a, siteNames)).ToList()));
        }
        return _pdf.Write(report.Name, blocks);
    }

    private static string GapText(TimeSpan gap)
    {
        var minutes = (int)Math.Round(gap.TotalMinutes);
        if (minutes >= 24 * 60) return $"{minutes / (24 * 60)} d {minutes / 60 % 24} h";
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
    }

    private static string UptimeText(PeriodTotals t) => t.UptimePercent is not { } u ? "—" : u >= 99.995 ? "100%" : Display.Num(u, 1) + "%";

    private sealed record Analysis(IReadOnlyList<NutrientGroupDto> Nutrients, IReadOnlyList<InRangeDto> InRange, IReadOnlyList<SummaryRowDto> Summary);

    /// <summary>The Reports page numbers of a month for the given provinces, from the saved day figures.</summary>
    private async Task<Analysis> AnalyseAsync(IReadOnlyList<Site> sites, DateTime first, DateTime last)
    {
        var now = _clock.UtcNow;
        var fromUtc = DailyFigures.DayStartUtc(first);
        var toUtc = DailyFigures.DayEndUtc(last);
        var visible = sites.Select(s => s.SiteId).ToHashSet();
        var sensors = (await _sensors.GetAllAsync()).Where(s => s.IsActive && visible.Contains(s.SiteId)).ToList();
        var days = await _daily.ForSensorsAsync(sensors, first, last);
        var allAlerts = await _alerts.GetAllAsync(visible);

        var nutrients = new List<NutrientGroupDto>();
        var inRange = new List<InRangeDto>();
        var summary = new List<SummaryRowDto>();
        var all = new PeriodTotals();

        foreach (var site in sites)
        {
            var siteSensors = sensors.Where(s => s.SiteId == site.SiteId).ToList();
            var t = new PeriodTotals();
            foreach (var s in siteSensors)
            {
                t.Add(days[s.SensorId], now, s.InstalledDate);
                all.Add(days[s.SensorId], now, s.InstalledDate);
            }
            double Avg(MeasuredParameter p) => t.Average(p) ?? 0;
            nutrients.Add(new NutrientGroupDto(site.Name, Math.Round(Avg(MeasuredParameter.Nitrogen)), Math.Round(Avg(MeasuredParameter.Phosphorus)), Math.Round(Avg(MeasuredParameter.Potassium))));
            inRange.Add(new InRangeDto(site.Name, t.InRangePercent, SiteMetrics.StatusFor(t.InRangePercent).Tone, false));

            var breaches = allAlerts.Count(a => a.SiteId == site.SiteId && a.AlertType != AlertType.Offline && a.RaisedAt >= fromUtc && a.RaisedAt < toUtc);
            var gap = PeriodTotals.LongestGap(siteSensors.Select(s => days[s.SensorId]), toUtc < now ? toUtc : now);
            summary.Add(new SummaryRowDto(site.Name, t.Readings.ToString("N0", Display.Culture), Display.Num(Avg(MeasuredParameter.Moisture), 1),
                Display.Num(Avg(MeasuredParameter.Temperature), 1), Display.Num(Avg(MeasuredParameter.Ph), 1), breaches.ToString(),
                t.Readings == 0 ? "—" : GapText(gap), t.Readings == 0 ? "—" : UptimeText(t)));
        }

        inRange.Add(new InRangeDto("All sites", all.InRangePercent, Tone.Off, true));
        return new Analysis(nutrients, inRange, summary);
    }
}

/// <summary>Colours of the three provinces on chips and charts.</summary>
public static class ProvincePalette
{
    public static string For(string siteId) => siteId switch { "FS" => "#10795A", "GP" => "#DE9A2E", "NW" => "#2F80C2", _ => "#8A9691" };
}

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly ILocationRepository _locations;
    private readonly IAuthGateway _gateway;
    private readonly IClock _clock;

    public UserService(IUserRepository users, ILocationRepository locations, IAuthGateway gateway, IClock clock)
    {
        _users = users;
        _locations = locations;
        _gateway = gateway;
        _clock = clock;
    }

    public async Task<UsersPageDto> GetPageAsync(UserContext ctx, string? search)
    {
        ctx.RequireAdmin();
        var now = _clock.UtcNow;
        var sites = await _locations.GetAllAsync();
        var all = await _users.GetAllAsync();
        var admins = all.Count(u => u.Role == UserRole.Administrator);
        var officers = all.Count - admins;

        var roles = new List<RoleCardDto>
        {
            new("Administrator", $"{admins} {(admins == 1 ? "account" : "accounts")}", Tone.Ok,
                new[] { "Read every reading in every province", "Create, edit and retire sensors", "Set threshold bands per province", "Manage accounts and province assignment" }),
            new("Field officer", $"{officers} {(officers == 1 ? "account" : "accounts")}", Tone.Info,
                new[] { "Read readings for assigned provinces only", "Acknowledge and resolve alerts", "Download reports for assigned provinces", "Cannot edit thresholds or accounts" })
        };

        IEnumerable<AppUser> q = all;
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || u.Email.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        var rows = q.OrderByDescending(u => u.Role == UserRole.Administrator).ThenBy(u => u.CreatedAt).ThenByDescending(u => u.LastActiveAt).Select(u => new UserRowDto(u.UserId, u.Name, u.Email, Display.RoleLabel(u.Role),
            u.Role == UserRole.Administrator || u.SiteIds.Count == sites.Count ? (sites.Count == 3 ? "All three provinces" : "All provinces") : string.Join(", ", sites.Where(s => u.SiteIds.Contains(s.SiteId)).Select(s => s.Name)),
            u.LastActiveAt is null ? "—" : Display.DayLabel(u.LastActiveAt.Value, now),
            u.Status.ToString(), u.Status == AccountStatus.Active ? Tone.Ok : u.Status == AccountStatus.Invited ? Tone.Warn : Tone.Off, u.UserId == ctx.UserId, string.Join(",", u.SiteIds))).ToList();

        return new UsersPageDto($"{all.Count} accounts across 2 roles", roles, rows, sites.Select(s => (s.SiteId, s.Name)).ToList());
    }

    public async Task<UserForm> GetFormAsync(UserContext ctx, string userId)
    {
        ctx.RequireAdmin();
        var u = await _users.GetAsync(userId) ?? throw new NotFoundException("That account was not found.");
        return new UserForm { UserId = u.UserId, Name = u.Name, Email = u.Email, Role = u.Role, SiteIds = u.SiteIds.ToList(), Status = u.Status };
    }

    /// <summary>
    /// Invites a person: creates the Firebase sign-in account (or reuses one that already exists for the email), writes users/{uid} with status Invited,
    /// and sends Firebase's "set your password" email. Field officers then sign in on the web and the mobile app; administrators on the web only.
    /// The status becomes Active at their first sign in.
    /// </summary>
    public async Task<UserSaveResult> CreateAsync(UserContext ctx, UserForm form)
    {
        ctx.RequireAdmin();
        var errors = await ValidateAsync(form, null);
        if (errors.Count > 0) return new UserSaveResult(false, errors);

        var email = form.Email!.Trim();
        var name = form.Name!.Trim();
        string uid;
        try
        {
            uid = await _gateway.FindUserIdAsync(email) ?? await _gateway.CreateAccountAsync(email, name);
        }
        catch (AuthGatewayException e)
        {
            return new UserSaveResult(false, new Dictionary<string, string> { ["Email"] = e.Message });
        }

        await _users.SaveAsync(new AppUser
        {
            UserId = uid, Name = name, Email = email, Role = form.Role, Status = AccountStatus.Invited,
            CreatedAt = _clock.UtcNow, SiteIds = await SitesForAsync(form)
        });

        try
        {
            await _gateway.SendInvitationAsync(email);
        }
        catch (AuthGatewayException e)
        {
            return new UserSaveResult(true, errors, $"The account was created, but the invitation email could not be sent: {e.Message} Use Resend invitation to try again.");
        }
        return new UserSaveResult(true, errors, $"Invitation sent to {email}. {WhereToSignIn(form.Role)}");
    }

    public async Task<UserSaveResult> ResendInvitationAsync(UserContext ctx, string userId)
    {
        ctx.RequireAdmin();
        var user = await _users.GetAsync(userId) ?? throw new NotFoundException("That account was not found.");
        if (user.Status != AccountStatus.Invited)
            return new UserSaveResult(false, new Dictionary<string, string> { ["Status"] = "Only an account that is still Invited can get the invitation again." });
        try
        {
            await _gateway.SendInvitationAsync(user.Email);
        }
        catch (AuthGatewayException e)
        {
            return new UserSaveResult(false, new Dictionary<string, string> { ["Email"] = e.Message });
        }
        return new UserSaveResult(true, new Dictionary<string, string>(), $"Invitation sent again to {user.Email}.");
    }

    /// <summary>Field officers use both applications; administrators only the web console (the database rules refuse them on the mobile app).</summary>
    private static string WhereToSignIn(UserRole role) => role == UserRole.Administrator
        ? "After setting a password they can sign in on the web console."
        : "After setting a password they can sign in on the web console and the mobile app.";

    /// <summary>Changes role, provinces and status. The last active administrator can never be removed or deactivated.</summary>
    public async Task<UserSaveResult> UpdateAsync(UserContext ctx, UserForm form)
    {
        ctx.RequireAdmin();
        var user = await _users.GetAsync(form.UserId ?? "") ?? throw new NotFoundException("That account was not found.");
        var errors = await ValidateAsync(form, user);

        var staysActiveAdmin = form.Role == UserRole.Administrator && form.Status == AccountStatus.Active;
        var otherAdmins = (await _users.GetAllAsync()).Count(u => u.UserId != user.UserId && u.Role == UserRole.Administrator && u.Status == AccountStatus.Active);
        if (user.Role == UserRole.Administrator && !staysActiveAdmin && otherAdmins == 0) errors["Role"] = "There must always be at least one active administrator.";
        if (user.UserId == ctx.UserId && (form.Role != user.Role || form.Status != AccountStatus.Active)) errors["Role"] = "You cannot change your own role or deactivate yourself.";
        if (errors.Count > 0) return new UserSaveResult(false, errors);

        var wasDeactivated = user.Status == AccountStatus.Deactivated;
        var name = form.Name!.Trim();
        var email = form.Email!.Trim();
        try
        {
            // the sign-in email lives in Firebase Authentication, so it changes there first
            if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase) || name != user.Name)
                await _gateway.UpdateAccountAsync(user.UserId, email, name);
            // a deactivated account can no longer sign in on the web or the mobile app
            if (form.Status == AccountStatus.Deactivated && !wasDeactivated) await _gateway.SetDisabledAsync(user.UserId, true);
            else if (form.Status != AccountStatus.Deactivated && wasDeactivated) await _gateway.SetDisabledAsync(user.UserId, false);
        }
        catch (AuthGatewayException e)
        {
            return new UserSaveResult(false, new Dictionary<string, string> { ["Email"] = e.Message });
        }

        user.Name = name;
        user.Email = email;
        user.Role = form.Role;
        user.Status = form.Status;
        user.SiteIds = await SitesForAsync(form);
        await _users.SaveAsync(user);
        return new UserSaveResult(true, errors);
    }

    private async Task<Dictionary<string, string>> ValidateAsync(UserForm form, AppUser? existing)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(form.Name) || form.Name.Trim().Length > 80) errors["Name"] = "Enter a name (up to 80 characters).";
        var email = form.Email?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) errors["Email"] = "Enter a valid email address.";
        else
        {
            var clash = await _users.GetByEmailAsync(email);
            if (clash is not null && clash.UserId != existing?.UserId) errors["Email"] = "Another account already uses this email.";
        }
        if (form.Role == UserRole.StandardUser)
        {
            var known = (await _locations.GetAllAsync()).Select(s => s.SiteId).ToHashSet();
            if (!form.SiteIds.Any(known.Contains)) errors["SiteIds"] = "Assign at least one province.";
        }
        return errors;
    }

    /// <summary>Administrators get every province; a Standard User gets exactly the chosen valid ones.</summary>
    private async Task<List<string>> SitesForAsync(UserForm form)
    {
        var known = (await _locations.GetAllAsync()).Select(s => s.SiteId).ToList();
        return form.Role == UserRole.Administrator ? known : form.SiteIds.Where(known.Contains).Distinct().ToList();
    }
}

public sealed class ProfileService : IProfileService
{
    private readonly IUserRepository _users;
    private readonly ILocationRepository _locations;
    private readonly IClock _clock;

    public ProfileService(IUserRepository users, ILocationRepository locations, IClock clock)
    {
        _users = users;
        _locations = locations;
        _clock = clock;
    }

    public async Task<ProfileDto> GetAsync(UserContext ctx)
    {
        var u = await _users.GetAsync(ctx.UserId) ?? throw new NotFoundException("Account not found.");
        var provinces = (await _locations.GetAllAsync()).Where(s => u.Role == UserRole.Administrator || u.SiteIds.Contains(s.SiteId)).Select(s => (s.Name, ProvincePalette.For(s.SiteId))).ToList();
        return new ProfileDto(u.Name, Display.Initials(u.Name), u.Email, Display.RoleLabel(u.Role), u.Role == UserRole.Administrator, provinces,
            u.LastActiveAt is null ? "—" : Display.DayLabel(u.LastActiveAt.Value, _clock.UtcNow), Display.LongDateLocal(u.CreatedAt),
            "Your password is kept by the sign-in service", u.NotifyBreach, u.NotifyOffline);
    }

    public async Task SaveNotificationsAsync(UserContext ctx, bool breach, bool offline)
    {
        var u = await _users.GetAsync(ctx.UserId) ?? throw new NotFoundException("Account not found.");
        u.NotifyBreach = breach;
        u.NotifyOffline = offline;
        await _users.SaveAsync(u);
    }
}
