// What this file is for: the reports page, user accounts page and profile page.
// How it fits in: Service folder, Dtos folder. A DTO is a small class that carries ready-to-show data from a service to a page.

using SensorGrid.Domain;

namespace SensorGrid.Service;

public sealed record NutrientGroupDto(string Province, double Nitrogen, double Phosphorus, double Potassium);
public sealed record InRangeDto(string Name, int Percent, Tone Tone, bool IsTotal);
public sealed record SummaryRowDto(string Province, string Readings, string Moisture, string Temperature, string Ph, string Breaches, string LongestGap, string Uptime);
public sealed record GeneratedReportDto(string ReportId, string Name, string Meta, string By);
public sealed record ReportsPageDto(string Subtitle, string Period, string PeriodLabel, IReadOnlyList<(string Value, string Label)> Periods,
    IReadOnlyList<NutrientGroupDto> Nutrients, IReadOnlyList<InRangeDto> InRange, IReadOnlyList<SummaryRowDto> Summary, IReadOnlyList<GeneratedReportDto> Generated);
public sealed record ReportFile(byte[] Content, string FileName, string ContentType);
public sealed record GenerateReportResult(bool Success, string? Error, string? ReportId);
public sealed record RoleCardDto(string Role, string Count, Tone Tone, IReadOnlyList<string> Permissions);
public sealed record UserRowDto(string UserId, string Name, string Email, string Role, string Provinces, string LastActive, string StatusLabel, Tone Tone, bool IsSelf, string SiteIds);
public sealed record UsersPageDto(string Subtitle, IReadOnlyList<RoleCardDto> Roles, IReadOnlyList<UserRowDto> Rows, IReadOnlyList<(string SiteId, string Name)> Provinces);
/// <summary>Result of saving an account: success, the error for each field, and an optional message to show (for example "the email could not be sent").</summary>
public sealed record UserSaveResult(bool Success, IReadOnlyDictionary<string, string> Errors, string? Notice = null);
public sealed record ProfileDto(string Name, string Initials, string Email, string RoleLabel, bool IsAdmin, IReadOnlyList<(string Name, string Colour)> Provinces,
    string LastSignIn, string Created, string PasswordChanged, bool NotifyBreach, bool NotifyOffline, bool NotifyDaily);
