// What this file is for: makes pages faster by remembering recent answers from the database for a few seconds.
// How it fits in: Repository folder. It wraps the real store (FirebaseRealtimeDatabaseStore) and is registered as IRealtimeDatabaseStore,
// so every repository uses it without knowing. Each Firebase call takes about 0.2 s, and one page used to read the same account,
// provinces and alerts two or three times; now the repeats come from memory.
//   - Rarely changing branches (provinces, sensors, thresholds, accounts) are remembered for 30 seconds.
//   - Alerts, notifications and each sensor's latest reading are remembered for 10 seconds, so they still look live
//     (the bell refreshes every 15 seconds and the live pages every 10).
//   - Time ranges of readings (QueryByKeyAsync) are never remembered.
// Every write made through the app forgets the paths it touched straight away, so the app always sees its own changes.
// Changes made elsewhere (the mobile app, the gateway) show up when the remembered copy runs out.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace SensorGrid.Repository;

public sealed class CachingRealtimeDatabaseStore : IRealtimeDatabaseStore
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(10);

    /// <summary>The first part of a path and how long its answers are remembered.</summary>
    private static readonly Dictionary<string, TimeSpan> Branches = new()
    {
        ["locations"] = Long, ["sensors"] = Long, ["sensor_locations"] = Long, ["thresholds"] = Long,
        ["users"] = Long, ["user_locations"] = Long, ["location_users"] = Long,
        ["alerts"] = Short, ["alert_actions"] = Short, ["notifications"] = Short
    };

    private readonly IRealtimeDatabaseStore _inner;

    // path -> (when it runs out, the JSON text). Text, not JsonNode, so every caller gets its own fresh copy to work with.
    private readonly ConcurrentDictionary<string, (DateTime Until, string? Json)> _remembered = new();

    public CachingRealtimeDatabaseStore(IRealtimeDatabaseStore inner) => _inner = inner;

    public async Task<JsonNode?> GetAsync(string path, CancellationToken ct = default)
    {
        var clean = Clean(path);
        var keep = HowLong(clean);
        if (keep is null) return await _inner.GetAsync(clean, ct);

        if (_remembered.TryGetValue(clean, out var hit) && hit.Until > DateTime.UtcNow)
            return hit.Json is null ? null : JsonNode.Parse(hit.Json);

        var node = await _inner.GetAsync(clean, ct);
        _remembered[clean] = (DateTime.UtcNow + keep.Value, node?.ToJsonString());
        return node;
    }

    // a period of readings changes every few seconds and is different every time, so it always goes to Firebase
    public Task<IReadOnlyList<KeyValuePair<string, JsonNode?>>> QueryByKeyAsync(string path, string? startAt = null, string? endAt = null, int? limitToLast = null, CancellationToken ct = default) =>
        _inner.QueryByKeyAsync(path, startAt, endAt, limitToLast, ct);

    public async Task SetAsync(string path, JsonNode? json, CancellationToken ct = default)
    {
        Forget(Clean(path));
        await _inner.SetAsync(path, json, ct);
        Forget(Clean(path)); // again, in case someone read the old value while the write was on its way
    }

    public async Task UpdateAsync(string basePath, IReadOnlyDictionary<string, JsonNode?> updates, CancellationToken ct = default)
    {
        var written = updates.Keys.Select(k => Clean(Clean(basePath) + "/" + Clean(k))).ToList();
        written.ForEach(Forget);
        await _inner.UpdateAsync(basePath, updates, ct);
        written.ForEach(Forget);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        Forget(Clean(path));
        await _inner.DeleteAsync(path, ct);
        Forget(Clean(path));
    }

    /// <summary>How long answers for this path are remembered, or null when it is never remembered.</summary>
    private static TimeSpan? HowLong(string path)
    {
        var parts = path.Split('/');
        if (parts.Length == 3 && parts[0] == "readings" && parts[2] == "latest") return Short; // readings/{sensor}/latest
        return Branches.TryGetValue(parts[0], out var keep) ? keep : null;
    }

    /// <summary>Forgets every remembered answer that contains the written path or sits inside it.</summary>
    private void Forget(string written)
    {
        foreach (var key in _remembered.Keys)
        {
            if (written.Length == 0 || key == written || key.StartsWith(written + "/", StringComparison.Ordinal) || written.StartsWith(key + "/", StringComparison.Ordinal))
                _remembered.TryRemove(key, out _);
        }
    }

    private static string Clean(string? path) => (path ?? "").Trim('/');
}