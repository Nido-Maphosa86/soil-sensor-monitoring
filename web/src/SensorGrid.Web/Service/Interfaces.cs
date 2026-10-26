// What this file is for: the list of things the app can do, one interface per area (IAlertService, IUserService...).
// How it fits in: Service folder. The Web project calls ONLY these interfaces, never a repository or the database store.
// Every method that reads or writes first checks the role and the assigned provinces of the person asking.
using SensorGrid.Domain;

namespace SensorGrid.Service;

public enum SignInOutcome { Success, Failed, Denied, Unavailable }

/// <summary>The result of a sign in: Failed gives one message for any wrong detail; Denied is a deactivated account ("Access denied"); Unavailable means Firebase could not be reached.</summary>
public sealed record SignInResult(SignInOutcome Outcome, UserContext? User);

public interface IAuthService
{
    Task<SignInResult> SignInAsync(string? email, string? password);

    /// <summary>Loads a fresh context for a signed in person. Null when the account no longer exists or is not active (checked on every request).</summary>
    Task<UserContext?> RefreshAsync(string userId);

    /// <summary>Changes the password. Returns error messages; empty means success.</summary>
    Task<IReadOnlyList<string>> ChangePasswordAsync(string userId, string? current, string? next, string? confirm);

    /// <summary>Always finishes the same way, whether or not the email exists.</summary>
    Task RequestPasswordResetAsync(string? email);
}

public interface IShellService
{
    Task<ShellDto> GetAsync(UserContext ctx);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(UserContext ctx);
    Task<MoistureChartDto> GetMoistureChartAsync(UserContext ctx, string? range);
}

public interface ISiteService
{
    Task<SitesPageDto> GetSitesAsync(UserContext ctx);
    Task<SiteDetailDto> GetDetailAsync(UserContext ctx, string siteId);

    /// <summary>Adds a site (administrators only) and gives it the starting threshold bands.</summary>
    Task<SiteSaveResult> AddAsync(UserContext ctx, SiteForm form);
}

public interface ISensorService
{
    Task<SensorsPageDto> GetPageAsync(UserContext ctx, string? siteId, string? search, string? state, int page);
    Task<IReadOnlyList<(string SiteId, string Name)>> ProvincesAsync(UserContext ctx);
    Task<RegisterSensorForm> NewFormAsync(UserContext ctx, string? siteId);
    Task<RegisterResult> RegisterAsync(UserContext ctx, RegisterSensorForm form);
}

public interface IReadingService
{
    /// <summary>The filter used when the page first opens: the last 7 days.</summary>
    ReadingFilter DefaultFilter();

    ReadingFilter FilterForRange(string? range, string? siteId = null);
    Task<ReadingsPageDto> GetPageAsync(UserContext ctx, ReadingFilter filter);
    Task<byte[]> ExportCsvAsync(UserContext ctx, ReadingFilter filter);
}

public interface IThresholdService
{
    Task<string> DefaultSiteIdAsync(UserContext ctx);
    Task<ThresholdEditorDto> GetEditorAsync(UserContext ctx, string siteId);
    Task<SaveThresholdsResult> SaveAsync(UserContext ctx, string siteId, string? basis, IReadOnlyDictionary<MeasuredParameter, (string? Min, string? Max)> input);
    Task ResetToDefaultsAsync(UserContext ctx, string siteId);
}

public interface IAlertService
{
    Task<AlertsPageDto> GetPageAsync(UserContext ctx, string? chip, string? search, string? siteId);
    Task AcknowledgeAsync(UserContext ctx, string alertId, string? note);
    Task ResolveAsync(UserContext ctx, string alertId, string? note);
    Task<AlertNoteDto> GetNoteAsync(UserContext ctx, string alertId);
    Task<byte[]> ExportCsvAsync(UserContext ctx);
}

public interface INotificationService
{
    Task<IReadOnlyList<BellItemDto>> GetBellAsync(UserContext ctx, int max = 8);
    Task MarkReadAsync(UserContext ctx, string alertId);
    Task MarkAllReadAsync(UserContext ctx);
}

public interface IReportService
{
    Task<ReportsPageDto> GetPageAsync(UserContext ctx, string? period);
    Task<GenerateReportResult> GenerateAsync(UserContext ctx, ReportKind kind, ReportFormat format, string? period);
    Task<ReportFile> DownloadAsync(UserContext ctx, string reportId);
}

public interface IUserService
{
    Task<UsersPageDto> GetPageAsync(UserContext ctx, string? search);
    Task<UserForm> GetFormAsync(UserContext ctx, string userId);
    Task<UserSaveResult> CreateAsync(UserContext ctx, UserForm form);
    Task<UserSaveResult> UpdateAsync(UserContext ctx, UserForm form);

    /// <summary>Sends the invitation email again to an account that is still Invited.</summary>
    Task<UserSaveResult> ResendInvitationAsync(UserContext ctx, string userId);
}

public interface IProfileService
{
    Task<ProfileDto> GetAsync(UserContext ctx);
    Task SaveNotificationsAsync(UserContext ctx, bool breach, bool offline, bool daily);
}

/// <summary>Looks at the newest data of every sensor and raises alerts (the same rules as the Cloud Function). Never duplicates an open alert.</summary>
public interface IAlertEvaluator
{
    /// <summary>Runs one check. Returns how many new alerts were raised.</summary>
    Task<int> EvaluateAsync();
}

public interface ICsvWriter
{
    byte[] Write(IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows);
}

public interface IPdfWriter
{
    byte[] Write(string title, IEnumerable<string> lines);
}
