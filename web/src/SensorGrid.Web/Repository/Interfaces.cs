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

    /// <summary>
    /// Deletes a site in ONE update: locations, thresholds, alerts and alert_actions of the site, its location_users, the site in each
    /// user's user_locations, and the listed notifications (those of the site's alerts). The site must have no sensors left.
    /// </summary>
    Task DeleteAsync(string locationId, IEnumerable<string> userIds, IEnumerable<(string UserId, string AlertId)> notifications, CancellationToken ct = default);
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

/// <summary>
/// sensors/{loc}/{sensor_id}, plus sensor_locations/{sensor_id} = loc. The gateway's rows carry no location, so the lookup tells the
/// security rules which province a sensor's readings belong to. Both are written together in one update.
/// </summary>
public interface ISensorRepository
{
    Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken ct = default);
    Task<Sensor?> GetAsync(string sensorId, CancellationToken ct = default);
    Task AddAsync(Sensor sensor, CancellationToken ct = default);

    /// <summary>Saves the settings of a registered sensor (edit, retire, reactivate). The code and the site never change.</summary>
    Task UpdateAsync(Sensor sensor, CancellationToken ct = default);

    /// <summary>
    /// Deletes a sensor's registration in ONE update: sensors and sensor_locations, the listed alerts of the sensor with their actions, and
    /// the listed notifications. The readings under readings/{sensor_id} (and daily_stats) are kept.
    /// </summary>
    Task DeleteAsync(Sensor sensor, IEnumerable<string> alertIds, IEnumerable<(string UserId, string AlertId)> notifications, CancellationToken ct = default);
}

/// <summary>When a sensor last reported, from readings/{sensor_id}/latest (written by the gateway).</summary>
public interface ISensorStatusRepository
{
    Task<SensorStatus?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default);
}

/// <summary>
/// readings/{sensor_id}/history/{push_id} and readings/{sensor_id}/latest, written by the gateway (a row about every 6 seconds).
/// Only the gateway writes them. Reads use a key range, never the whole branch.
/// </summary>
public interface IReadingRepository
{
    /// <summary>The readings of one sensor between two UTC times (inclusive), oldest first.</summary>
    Task<IReadOnlyList<Reading>> QueryAsync(string locationId, string sensorId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// One page of a sensor's readings between two UTC times, with their keys (push IDs), for the Readings log. Reads only what it returns:
    /// without a key, the newest count rows; with beforeKey, the newest count rows older than that key; with afterKey, the oldest count rows
    /// newer than that key. Always oldest first.
    /// </summary>
    Task<IReadOnlyList<(string Key, Reading Reading)>> PageAsync(string locationId, string sensorId, DateTime fromUtc, DateTime toUtc,
        string? beforeKey, string? afterKey, int count, CancellationToken ct = default);

    Task<Reading?> LatestAsync(string locationId, string sensorId, CancellationToken ct = default);

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

/// <summary>daily_stats/{sensor_id}/{yyyy-MM-dd}: the saved figures of finished days. Only the web server writes them.</summary>
public interface IDailyStatsRepository
{
    /// <summary>The saved days of one sensor from firstDay to lastDay (both included), oldest first. Days not saved yet are simply missing.</summary>
    Task<IReadOnlyList<DailyStats>> GetRangeAsync(string sensorId, DateTime firstDay, DateTime lastDay, CancellationToken ct = default);
    Task SaveAsync(DailyStats stats, CancellationToken ct = default);
}

