// What this file is for: the ONE door to the database. Every repository reads and writes through this interface.
// How it fits in: Repository folder. It looks like the Firebase Realtime Database REST API (paths, key ranges, multi-path updates),
// so the repositories never see HTTP, addresses or tokens.
using System.Text.Json.Nodes; // JsonNode: a piece of JSON we can read and build in code

namespace SensorGrid.Repository;

/// <summary>Raised when the database cannot be read or written. The message always names the path.</summary>
public class RealtimeDatabaseException : Exception
{
    public RealtimeDatabaseException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>What the app can ask the database to do. The Firebase version is written in the next commit.</summary>
public interface IRealtimeDatabaseStore
{
    /// <summary>Reads the JSON at a path. Returns null when nothing is stored there. An empty path is the whole tree.</summary>
    Task<JsonNode?> GetAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Children of a path ordered by key (REST: orderBy="$key"). startAt and endAt are inclusive; limitToLast keeps the last N.
    /// Used for the gateway's readings, whose keys are push IDs that start with their time, so a period is a key range.
    /// </summary>
    Task<IReadOnlyList<KeyValuePair<string, JsonNode?>>> QueryByKeyAsync(string path, string? startAt = null, string? endAt = null, int? limitToLast = null, CancellationToken ct = default);

    /// <summary>Replaces the JSON at a path (REST PUT). A null value deletes the path.</summary>
    Task SetAsync(string path, JsonNode? json, CancellationToken ct = default);

    /// <summary>
    /// A multi-path update, all or nothing (REST PATCH). Each key is a path relative to basePath (use "" for the root),
    /// each value the new JSON, or null to delete that path.
    /// </summary>
    Task UpdateAsync(string basePath, IReadOnlyDictionary<string, JsonNode?> updates, CancellationToken ct = default);

    /// <summary>Deletes everything at a path.</summary>
    Task DeleteAsync(string path, CancellationToken ct = default);
}