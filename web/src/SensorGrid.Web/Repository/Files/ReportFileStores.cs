// What this file is for: saves and reads the report files (PDF and CSV), behind one interface (IReportFileStore).
// How it fits in: Repository folder. LocalDiskReportFileStore keeps files on the web server (free Spark plan); CloudStorageReportFileStore needs the Blaze plan.
// The Cloud Storage store uses the Cloud Storage JSON API with the service-account token. The report record in the database only holds the path.
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace SensorGrid.Repository;

public interface IReportFileStore
{
    Task SaveAsync(string path, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>Returns the file, or null when it does not exist.</summary>
    Task<byte[]?> ReadAsync(string path, CancellationToken ct = default);
}

/// <summary>Files go to App_Data/reports next to the running app (used while SensorGrid:UseCloudStorageForReports is false). Only plain file names are accepted, so no path can escape the folder.</summary>
public sealed class LocalDiskReportFileStore : IReportFileStore
{
    private static readonly Regex Safe = new(@"^[A-Za-z0-9._\-/]+$", RegexOptions.Compiled);
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "App_Data", "reports");

    public async Task SaveAsync(string path, byte[] content, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllBytesAsync(Resolve(path), content, ct);
    }

    public async Task<byte[]?> ReadAsync(string path, CancellationToken ct = default)
    {
        var file = Resolve(path);
        return File.Exists(file) ? await File.ReadAllBytesAsync(file, ct) : null;
    }

    private string Resolve(string path)
    {
        if (!Safe.IsMatch(path) || path.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("That file path is not allowed.");
        return Path.Combine(_folder, path.Replace('/', '_'));
    }
}

/// <summary>
/// Cloud Storage through its JSON API.
/// CONNECT FIREBASE HERE: needs SensorGrid:Firebase:StorageBucket and the service-account token. Cloud Storage needs the Blaze plan (open decision 22.7.4).
/// </summary>
public sealed class CloudStorageReportFileStore : IReportFileStore
{
    private readonly HttpClient _http;
    private readonly string _bucket;
    private readonly IAccessTokenProvider _tokens;

    public CloudStorageReportFileStore(HttpClient http, FirebaseSettings settings, IAccessTokenProvider tokens)
    {
        _http = http;
        _bucket = settings.StorageBucket;
        _tokens = tokens;
    }

    public async Task SaveAsync(string path, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://storage.googleapis.com/upload/storage/v1/b/{_bucket}/o?uploadType=media&name={Uri.EscapeDataString(path)}")
        {
            Content = new ByteArrayContent(content)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Headers.Authorization = new("Bearer", await _tokens.GetAccessTokenAsync(ct));
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new RealtimeDatabaseException($"Cloud Storage could not save {path} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");
    }

    public async Task<byte[]?> ReadAsync(string path, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://storage.googleapis.com/storage/v1/b/{_bucket}/o/{Uri.EscapeDataString(path)}?alt=media");
        request.Headers.Authorization = new("Bearer", await _tokens.GetAccessTokenAsync(ct));
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new RealtimeDatabaseException($"Cloud Storage could not read {path} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
