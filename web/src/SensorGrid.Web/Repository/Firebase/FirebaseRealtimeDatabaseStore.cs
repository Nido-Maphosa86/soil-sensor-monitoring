// What this file is for: the database store. It talks to the Firebase Realtime Database REST API: <DatabaseUrl>/<path>.json with a service-account access token.
// How it fits in: Repository folder. It is the only class that knows about HTTP and the addresses of the database.
// CONNECT FIREBASE HERE: it needs SensorGrid:Firebase:DatabaseUrl and the access token provider. The web server's token skips Firebase Security Rules,
// so the Service layer checks the role and the assigned provinces before every read and write.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace SensorGrid.Repository;

/// <summary>IRealtimeDatabaseStore over the Firebase REST API.</summary>
public sealed class FirebaseRealtimeDatabaseStore : IRealtimeDatabaseStore
{
    private readonly HttpClient _http;
    private readonly string _databaseUrl;
    private readonly string _baseQuery; // "ns=project" when the emulator is used, otherwise empty
    private readonly IAccessTokenProvider _tokens;
    private readonly ILogger<FirebaseRealtimeDatabaseStore> _log;

    public FirebaseRealtimeDatabaseStore(HttpClient http, FirebaseSettings settings, IAccessTokenProvider tokens, ILogger<FirebaseRealtimeDatabaseStore> log)
    {
        _http = http;
        _log = log;
        // The emulator address carries the database name as a query: http://127.0.0.1:9000/?ns=sensorgrid-dev
        var parts = settings.DatabaseUrl.Trim().Split('?', 2);
        _databaseUrl = parts[0].TrimEnd('/');                         // the address without a trailing slash
        _baseQuery = parts.Length > 1 ? parts[1].Trim('&') : "";       // anything after the "?"
        _tokens = tokens;
    }

    /// <summary>GET: read the JSON at a path ("null" from Firebase means nothing is stored there).</summary>
    public async Task<JsonNode?> GetAsync(string path, CancellationToken ct = default)
    {
        var text = await SendAsync(HttpMethod.Get, path, "", null, ct);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }

    /// <summary>GET with orderBy="$key": the children of a path in key order, optionally a range and only the last few.</summary>
    public async Task<IReadOnlyList<KeyValuePair<string, JsonNode?>>> QueryByKeyAsync(string path, string? startAt = null, string? endAt = null, int? limitToLast = null, CancellationToken ct = default)
    {
        // The values of these parameters must be JSON, so strings are quoted: orderBy="$key"&startAt="202609010000"
        var q = new StringBuilder("&orderBy=" + Uri.EscapeDataString("\"$key\""));
        if (startAt is not null) q.Append("&startAt=").Append(Uri.EscapeDataString(JsonSerializer.Serialize(startAt)));
        if (endAt is not null) q.Append("&endAt=").Append(Uri.EscapeDataString(JsonSerializer.Serialize(endAt)));
        if (limitToLast is { } n) q.Append("&limitToLast=").Append(n);

        var text = await SendAsync(HttpMethod.Get, path, q.ToString(), null, ct);
        var list = new List<KeyValuePair<string, JsonNode?>>();
        if (!string.IsNullOrWhiteSpace(text) && JsonNode.Parse(text) is JsonObject o)
            foreach (var kv in o) list.Add(new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value?.DeepClone())); // the database returns them ordered by key
        return list;
    }

    /// <summary>PUT: replace a path. Setting null means delete.</summary>
    public async Task SetAsync(string path, JsonNode? json, CancellationToken ct = default)
    {
        if (json is null)
        {
            await DeleteAsync(path, ct);
            return;
        }
        await SendAsync(HttpMethod.Put, path, "", json.ToJsonString(), ct);
    }

    /// <summary>PATCH: write several paths in one request; Firebase applies all of them or none.</summary>
    public async Task UpdateAsync(string basePath, IReadOnlyDictionary<string, JsonNode?> updates, CancellationToken ct = default)
    {
        var body = new JsonObject();
        foreach (var (rel, value) in updates) body[rel] = value?.DeepClone(); // a null value deletes that path
        await SendAsync(HttpMethod.Patch, basePath, "", body.ToJsonString(), ct);
    }

    /// <summary>DELETE: remove a path.</summary>
    public Task DeleteAsync(string path, CancellationToken ct = default) => SendAsync(HttpMethod.Delete, path, "", null, ct);

    /// <summary>Sends one request. Every failure names the method and the path.</summary>
    private async Task<string> SendAsync(HttpMethod method, string path, string extraQuery, string? jsonBody, CancellationToken ct)
    {
        var clean = (path ?? "").Trim('/'); // "alerts/GP/" becomes "alerts/GP"
        var watch = Stopwatch.StartNew();
        try
        {
            var token = await _tokens.GetAccessTokenAsync(ct);
            // The token goes in the Authorization header (not the address), so it never shows up in logs.
            var query = (_baseQuery + extraQuery).TrimStart('&');
            var url = $"{_databaseUrl}/{clean}.json" + (query.Length == 0 ? "" : "?" + query); // for example .../alerts/GP.json
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new("Bearer", token);
            if (jsonBody is not null) request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new RealtimeDatabaseException($"Firebase {method} /{clean} failed ({(int)response.StatusCode}): {text}");
            // how long each call took and how much came back (shown while developing, to find what makes a page slow)
            _log.LogDebug("Firebase {Method} /{Path}: {Ms} ms, {Kb} KB", method, clean, watch.ElapsedMilliseconds, text.Length / 1024);
            return text;
        }
        catch (RealtimeDatabaseException)
        {
            throw; // already has a clear message; pass it on unchanged
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // no internet, a timeout or a broken answer: wrap it in our own error that names the path
            throw new RealtimeDatabaseException($"Firebase {method} /{clean} could not be completed: {e.Message}", e);
        }
    }
}