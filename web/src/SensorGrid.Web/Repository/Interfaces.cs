// What this file is for: the list of data operations the app needs (one interface per area).
// How it fits in: Repository folder. The Service layer only knows these interfaces. All of them are written against IRealtimeDatabaseStore (the Firebase Realtime Database).
using SensorGrid.Domain;

namespace SensorGrid.Repository;

/// <summary>locations/{location_id}</summary>
public interface ILocationRepository
{
    Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct = default);
    Task<Site?> GetAsync(string locationId, CancellationToken ct = default);
    Task SaveAsync(Site site, CancellationToken ct = default);
}

/// <summary>users/{uid}, user_locations/{uid}/{loc} and location_users/{loc}/{uid}. The three are always written together in one update.</summary>
public interface IUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default);
    Task<AppUser?> GetAsync(string uid, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Creates or replaces a user and its location links (locations that were removed are unlinked).</summary>
    Task SaveAsync(AppUser user, CancellationToken ct = default);

    Task TouchLastActiveAsync(string uid, DateTime utc, CancellationToken ct = default);

    /// <summary>The user ids assigned to a location (location_users).</summary>
    Task<IReadOnlyList<string>> UserIdsForLocationAsync(string locationId, CancellationToken ct = default);
}

/// <summary>sensors/{loc}/{sensor_id}</summary>
public interface ISensorRepository
{
    Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken ct = default);
    Task<Sensor?> GetAsync(string sensorId, CancellationToken ct = default);
    Task AddAsync(Sensor sensor, CancellationToken ct = default);
}

/// <summary>sensor_status/{loc}/{sensor}/{key}. Written by the gateway; the newest key is the current health.</summary>
public interface ISensorStatusRepository
{
    Task<SensorStatus?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default);
    Task AddAsync(SensorStatus status, CancellationToken ct = default);
}

/// <summary>readings/{loc}/{sensor}/{key}. Written by the gateway. Reads use a key range, never the whole branch.</summary>
public interface IReadingRepository
{
    /// <summary>The readings of one sensor between two UTC times (inclusive), oldest first.</summary>
    Task<IReadOnlyList<Reading>> QueryAsync(string locationId, string sensorId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task<Reading?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default);

    /// <summary>Writes many readings in one update (used by the seeder; the real gateway writes its own).</summary>
    Task AddRangeAsync(IEnumerable<Reading> readings, CancellationToken ct = default);
}

/// <summary>thresholds/{loc}/{parameter}</summary>
public interface IThresholdRepository
{
    Task<IReadOnlyList<Threshold>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Saves bands together in one update, so a location's bands are never half saved.</summary>
    Task SaveAsync(IEnumerable<Threshold> thresholds, CancellationToken ct = default);
}

/// <summary>alerts/{loc}/{alert_id}, alert_actions/{loc}/{alert_id}/... and the notifications written with a new alert.</summary>
public interface IAlertRepository
{
    /// <summary>The alerts of some locations (all when null), with their actions.</summary>
    Task<IReadOnlyList<Alert>> GetAllAsync(IEnumerable<string>? locationIds = null, CancellationToken ct = default);

    Task<Alert?> GetAsync(string locationId, string alertId, CancellationToken ct = default);

    /// <summary>Creates an alert and one notification per listed person, together in ONE update. Only the alert evaluator creates alerts.</summary>
    Task AddAsync(Alert alert, IEnumerable<Notification> notifications, CancellationToken ct = default);

    /// <summary>Writes the action (acknowledged or resolved) AND the new state in ONE update.</summary>
    Task SaveActionAsync(Alert alert, bool resolved, CancellationToken ct = default);
}

/// <summary>notifications/{uid}/{alert_id}</summary>
public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ForUserAsync(string uid, CancellationToken ct = default);
    Task MarkReadAsync(string uid, IEnumerable<string> alertIds, DateTime readAtUtc, CancellationToken ct = default);
}

/// <summary>reports/{report_id} and report_locations/{report_id}/{loc}</summary>
public interface IReportRepository
{
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);
    Task<Report?> GetAsync(string reportId, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
}

/// <summary>data_loads/{load_id}. Only the gateway writes these; the seeder writes one so the branch exists.</summary>
public interface IDataLoadRepository
{
    Task AddAsync(DataLoad load, CancellationToken ct = default);
}
