// What this file is for: the Reports page and file downloads, user account management (administrators only), and the Profile page.
// How it fits in: Service folder. Reports only ever contain data from the provinces of the person asking; report files are saved through IReportFileStore
// (Cloud Storage, or App_Data/reports on the web server) and the report record in the database only holds the file path.
using System.Globalization;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class ReportService : IReportService
{
    private readonly DataScope _scope;
    private readonly SensorLookup _lookup;
    private readonly IAlertRepository _alerts;
    private readonly IReportRepository _reports;
    private readonly IReportFileStore _files;
    private readonly IUserRepository _users;
    private readonly Analytics _analytics;
    private readonly ICsvWriter _csv;
    private readonly IPdfWriter _pdf;
    private readonly IClock _clock;

    public ReportService(DataScope scope, SensorLookup lookup, IAlertRepository alerts, IReportRepository reports, IReportFileStore files, IUserRepository users,
        Analytics analytics, ICsvWriter csv, IPdfWriter pdf, IClock clock)
    {
        _scope = scope;
        _lookup = lookup;
        _alerts = alerts;
        _reports = reports;
        _files = files;
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
        var data = await AnalyseAsync(ctx, period);
        var sites = await _scope.VisibleSitesAsync(ctx);
        var visibleIds = sites.Select(s => s.SiteId).ToHashSet();
        var userNames = (await _users.GetAllAsync()).ToDictionary(u => u.UserId, u => u.Name);

        var generated = (await _reports.GetAllAsync())
            .Where(r => ctx.IsAdmin || r.GeneratedBy == ctx.UserId || (r.SiteIds.Count > 0 && r.SiteIds.All(visibleIds.Contains)))
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new GeneratedReportDto(r.ReportId, r.Name, $"{TreeText(r.Format)} · {Display.Size(r.SizeBytes)}",
                $"Generated {Display.Local(r.GeneratedAt).ToString("d MMMM", Display.Culture)} by {userNames.GetValueOrDefault(r.GeneratedBy, "unknown")}")).ToList();

        var words = new[] { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        var subtitle = $"Nutrient and moisture analysis across {(sites.Count < words.Length ? words[sites.Count] : sites.Count.ToString())} {(sites.Count == 1 ? "site" : "sites")}";
        return new ReportsPageDto(subtitle, period, periods.First(p => p.Value == period).Label, periods, data.Nutrients, data.InRange, data.Summary, generated);
    }

    /// <summary>Builds the file, saves it, then records the report and its locations together. Raw readings are CSV only.</summary>
    public async Task<GenerateReportResult> GenerateAsync(UserContext ctx, ReportKind kind, ReportFormat format, string? period)
    {
        if (period is null || Periods().All(p => p.Value != period)) return new GenerateReportResult(false, "Choose a month.", null);
        if (kind == ReportKind.RawReadings && format == ReportFormat.Pdf) return new GenerateReportResult(false, "Raw readings can only be exported as CSV.", null);

        var label = Periods().First(p => p.Value == period).Label;
        var title = kind switch { ReportKind.NutrientSummary => "Monthly nutrient summary", ReportKind.RawReadings => "Raw readings export", _ => "Threshold breach log" };
        var start = DateTime.ParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture);
        var sites = (await _scope.VisibleSitesAsync(ctx)).Select(s => s.SiteId).ToList();
        var id = PushIds.At(_clock.UtcNow);
        var ext = format == ReportFormat.Csv ? "csv" : "pdf";

        var report = new Report
        {
            ReportId = id, GeneratedBy = ctx.UserId, Name = $"{title} — {label}", Kind = kind, Format = format, PeriodStart = start,
            PeriodEnd = start.AddMonths(1).AddDays(-1), GeneratedAt = _clock.UtcNow, FilePath = $"reports/{id}.{ext}", SiteIds = sites
        };
        var bytes = await BuildAsync(ctx, report, period);
        report.SizeBytes = bytes.LongLength;
        await _files.SaveAsync(report.FilePath, bytes, format == ReportFormat.Csv ? "text/csv" : "application/pdf");
        await _reports.AddAsync(report);
        return new GenerateReportResult(true, null, id);
    }

    /// <summary>The stored file. Only an administrator, the person who made it, or someone who can open all its provinces may download it.</summary>
    public async Task<ReportFile> DownloadAsync(UserContext ctx, string reportId)
    {
        var report = await _reports.GetAsync(reportId) ?? throw new NotFoundException("That report was not found.");
        if (!ctx.IsAdmin && report.GeneratedBy != ctx.UserId && !report.SiteIds.All(ctx.CanSee)) throw new NotFoundException("That report was not found.");
        var content = await _files.ReadAsync(report.FilePath) ?? throw new NotFoundException("The file of this report is not available.");
        var ext = report.Format == ReportFormat.Csv ? "csv" : "pdf";
        return new ReportFile(content, $"{report.Name.Replace(" — ", "-").Replace(' ', '-')}.{ext}", report.Format == ReportFormat.Csv ? "text/csv" : "application/pdf");
    }

    private static string TreeText(ReportFormat f) => f == ReportFormat.Pdf ? "PDF" : "CSV";

    private async Task<byte[]> BuildAsync(UserContext ctx, Report report, string period)
    {
        var data = await AnalyseAsync(ctx, period);
        var (from, to) = Window(period);
        var sites = await _scope.VisibleSitesAsync(ctx);
        var siteNames = sites.ToDictionary(s => s.SiteId, s => s.Name);
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).ToList();

        List<string> header;
        List<List<string>> rows;
        switch (report.Kind)
        {
            case ReportKind.RawReadings:
            {
                header = new List<string> { "Timestamp", "Province", "Sensor" };
                header.AddRange(ParameterCatalog.Readings.Select(i => $"{i.Short} ({i.Unit})"));
                var all = (await _lookup.ReadingsAsync(sensors, from, to)).Values.SelectMany(x => x).OrderBy(r => r.Timestamp).ThenBy(r => r.SensorId, StringComparer.Ordinal);
                rows = all.Select(r => new[] { Display.Local(r.Timestamp).ToString("yyyy-MM-dd HH:mm:ss"), siteNames[r.SiteId], r.SensorId }
                    .Concat(ParameterCatalog.Readings.Select(i => r.ValueOf(i.Parameter).ToString(CultureInfo.InvariantCulture))).ToList()).ToList();
                break;
            }
            case ReportKind.BreachLog:
            {
                header = new List<string> { "Raised", "Type", "Severity", "Sensor", "Parameter", "Reading", "Limit", "Status" };
                rows = (await _alerts.GetAllAsync(sites.Select(s => s.SiteId))).Where(a => a.RaisedAt >= from && a.RaisedAt < to).OrderBy(a => a.RaisedAt).Select(a =>
                    new List<string>
                    {
                        Display.Local(a.RaisedAt).ToString("yyyy-MM-dd HH:mm"), a.AlertType.ToString(), a.AlertType == AlertType.Offline ? "—" : a.Severity.ToString(), a.SensorId,
                        a.Parameter is { } p ? ParameterCatalog.Get(p).Short : "—", a.ReadingValue?.ToString(CultureInfo.InvariantCulture) ?? "—",
                        a.ThresholdValue is { } t ? (a.ThresholdIsMax ? "max " : "min ") + t.ToString(CultureInfo.InvariantCulture) : "—", a.Status.ToString()
                    }).ToList();
                break;
            }
            default:
                header = new List<string> { "Province", "Readings", "Moisture avg %", "Temp avg °C", "pH avg", "Nitrogen avg mg/kg", "Phosphorus avg mg/kg", "Potassium avg mg/kg", "Breaches", "Longest gap", "Uptime" };
                rows = data.Summary.Select(s =>
                {
                    var n = data.Nutrients.First(x => x.Province == s.Province);
                    return new List<string> { s.Province, s.Readings, s.Moisture, s.Temperature, s.Ph, Display.Num(n.Nitrogen, 0), Display.Num(n.Phosphorus, 0), Display.Num(n.Potassium, 0), s.Breaches, s.LongestGap, s.Uptime };
                }).ToList();
                break;
        }

        if (report.Format == ReportFormat.Csv) return _csv.Write(header, rows);
        var lines = new List<string> { string.Join("  |  ", header) };
        lines.AddRange(rows.Take(300).Select(r => string.Join("  |  ", r)));
        if (rows.Count == 0) lines.Add("No data is stored for this period.");
        return _pdf.Write(report.Name, lines);
    }

    /// <summary>The month as UTC times: from the start of the month (South African time) to its end.</summary>
    private static (DateTime From, DateTime To) Window(string period)
    {
        var start = DateTime.ParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture);
        return (DateTime.SpecifyKind(start - Display.SouthAfrica, DateTimeKind.Utc), DateTime.SpecifyKind(start.AddMonths(1) - Display.SouthAfrica, DateTimeKind.Utc));
    }

    private sealed record Analysis(IReadOnlyList<NutrientGroupDto> Nutrients, IReadOnlyList<InRangeDto> InRange, IReadOnlyList<SummaryRowDto> Summary);

    /// <summary>All the numbers of one month for the provinces the person may see.</summary>
    private async Task<Analysis> AnalyseAsync(UserContext ctx, string period)
    {
        var now = _clock.UtcNow;
        var (from, fullTo) = Window(period);
        var to = fullTo < now ? fullTo : now;
        var sites = await _scope.VisibleSitesAsync(ctx);
        var sensors = (await _scope.VisibleSensorsAsync(ctx)).Where(s => s.IsActive).ToList();
        var bandsBySite = await _analytics.AllBandsAsync();
        var allAlerts = await _alerts.GetAllAsync(sites.Select(s => s.SiteId));
        var minutes = Math.Max(1, (to - from).TotalMinutes);
        var byDay = to > from ? await _lookup.ReadingsAsync(sensors, from, to) : sensors.ToDictionary(s => s.SensorId, _ => (IReadOnlyList<Reading>)new List<Reading>());

        var nutrients = new List<NutrientGroupDto>();
        var inRange = new List<InRangeDto>();
        var summary = new List<SummaryRowDto>();
        int totalReadings = 0, totalInRange = 0;

        foreach (var site in sites)
        {
            var siteSensors = sensors.Where(s => s.SiteId == site.SiteId).ToList();
            var mine = siteSensors.SelectMany(s => byDay[s.SensorId]).ToList();
            double Avg(Func<Reading, double> f) => mine.Count == 0 ? 0 : mine.Average(f);
            nutrients.Add(new NutrientGroupDto(site.Name, Math.Round(Avg(r => r.Nitrogen)), Math.Round(Avg(r => r.Phosphorus)), Math.Round(Avg(r => r.Potassium))));

            var bands = bandsBySite.GetValueOrDefault(site.SiteId) ?? new();
            var good = mine.Count(r => Analytics.IsInRange(r, bands));
            totalReadings += mine.Count;
            totalInRange += good;
            var pct = mine.Count == 0 ? 0 : (int)Math.Round(100.0 * good / mine.Count);
            inRange.Add(new InRangeDto(site.Name, pct, SiteMetrics.StatusFor(pct).Tone, false));

            var breaches = allAlerts.Count(a => a.SiteId == site.SiteId && a.AlertType != AlertType.Offline && a.RaisedAt >= from && a.RaisedAt < fullTo);
            var expected = siteSensors.Count * minutes / 30.0;
            var uptime = expected <= 0 ? 0 : Math.Min(100.0, 100.0 * mine.Count / expected);
            summary.Add(new SummaryRowDto(site.Name, mine.Count.ToString("N0", Display.Culture), Display.Num(Avg(r => r.Moisture), 1), Display.Num(Avg(r => r.Temperature), 1),
                Display.Num(Avg(r => r.Ph), 1), breaches.ToString(), mine.Count == 0 ? "—" : LongestGap(siteSensors, byDay, to),
                mine.Count == 0 ? "—" : uptime >= 99.995 ? "100%" : Display.Num(uptime, 1) + "%"));
        }

        var overall = totalReadings == 0 ? 0 : (int)Math.Round(100.0 * totalInRange / totalReadings);
        inRange.Add(new InRangeDto("All sites", overall, Tone.Off, true));
        return new Analysis(nutrients, inRange, summary);
    }

    /// <summary>The longest stretch without a reading from any sensor, counting the silence up to the end of the period.</summary>
    private static string LongestGap(IEnumerable<Sensor> sensors, Dictionary<string, IReadOnlyList<Reading>> byDay, DateTime to)
    {
        var longest = TimeSpan.Zero;
        foreach (var sensor in sensors)
        {
            var times = byDay[sensor.SensorId].Select(r => r.Timestamp).OrderBy(t => t).ToList();
            if (times.Count == 0) continue;
            for (var i = 1; i < times.Count; i++) if (times[i] - times[i - 1] > longest) longest = times[i] - times[i - 1];
            if (to - times[^1] > longest) longest = to - times[^1];
        }
        var minutes = (int)Math.Round(longest.TotalMinutes);
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
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
            "Your password is kept by the sign-in service", u.NotifyBreach, u.NotifyOffline, u.NotifyDaily);
    }

    public async Task SaveNotificationsAsync(UserContext ctx, bool breach, bool offline, bool daily)
    {
        var u = await _users.GetAsync(ctx.UserId) ?? throw new NotFoundException("Account not found.");
        u.NotifyBreach = breach;
        u.NotifyOffline = offline;
        u.NotifyDaily = daily;
        await _users.SaveAsync(u);
    }
}
