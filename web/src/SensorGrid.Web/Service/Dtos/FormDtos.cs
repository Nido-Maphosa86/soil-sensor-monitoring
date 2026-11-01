// What this file is for: the values typed on forms (register sensor, create or edit a user) before they are checked.
// How it fits in: Service folder, Dtos folder. Controllers fill these from the posted form and hand them to a service, which validates them.
using SensorGrid.Domain;

namespace SensorGrid.Service;

/// <summary>The values typed on the Register sensor form.</summary>
public sealed class RegisterSensorForm
{
    public string? SensorId { get; set; }
    public string? SiteId { get; set; }
    public DateTime? InstalledDate { get; set; }
    public int? DepthCm { get; set; }
    public int? ModbusSlaveId { get; set; }
    public bool IncludeInAverage { get; set; } = true;
}

/// <summary>The values typed when inviting or editing an account.</summary>
public sealed class UserForm
{
    public string? UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.StandardUser;
    public List<string> SiteIds { get; set; } = new();
    public AccountStatus Status { get; set; } = AccountStatus.Active;
}

/// <summary>The Add site dialog on W3. The site id is the two letter prefix of its sensor codes (FS for FS-01), so it must match what the sensor gateway sends.</summary>
public sealed class SiteForm
{
    public string? SiteId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Crop { get; set; } = "Pigeon pea";
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public DateTime? PlantingDate { get; set; }
}

/// <summary>Result of saving a site: success, or the error for each field.</summary>
public sealed record SiteSaveResult(bool Success, IReadOnlyDictionary<string, string> Errors);

/// <summary>
/// The Edit site page: the form, and what is still attached to the site. Delete is only offered once SensorCount is 0.
/// </summary>
public sealed record SiteEditDto(string SiteId, SiteForm Form, int SensorCount, int UserCount, int AlertCount);

/// <summary>The Edit sensor page: the form (the code and the site cannot change), whether it is retired, and its last reading.</summary>
public sealed record SensorEditDto(RegisterSensorForm Form, string SiteName, bool IsActive, string LastReading, int OpenAlerts, int AlertCount);

/// <summary>Result of an edit or a delete: success, or the error for each field ("" = the whole form).</summary>
public sealed record ChangeResult(bool Success, IReadOnlyDictionary<string, string> Errors, SlaveConflict? Conflict = null)
{
    public static readonly ChangeResult Ok = new(true, new Dictionary<string, string>());
    public static ChangeResult Fail(string field, string message) => new(false, new Dictionary<string, string> { [field] = message });
}
