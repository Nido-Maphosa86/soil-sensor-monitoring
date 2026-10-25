// What this file is for: gets a Google access token for the web server from the service-account key file, and keeps it until 5 minutes before it expires.
// How it fits in: Repository folder. The Firebase database store, the Firebase sign-in gateway and the Cloud Storage report store all ask it for a token.
// CONNECT FIREBASE HERE: it only needs SensorGrid:Firebase:ServiceAccountKeyPath. It signs a short-lived JWT (RS256) with the key and exchanges it at
// oauth2.googleapis.com/token. No NuGet package is needed: only HttpClient and System.Security.Cryptography. The key never leaves the server.
using System.Security.Cryptography; // RSA signing
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SensorGrid.Repository;

/// <summary>Anything that can hand out an access token for Google and Firebase calls.</summary>
public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>Gets the token from Google using the service-account key file.</summary>
public sealed class GoogleAccessTokenProvider : IAccessTokenProvider
{
    // What the token is allowed to do: Realtime Database, Cloud Storage and the Identity Toolkit admin calls.
    private const string Scopes = "https://www.googleapis.com/auth/firebase.database https://www.googleapis.com/auth/userinfo.email " +
                                  "https://www.googleapis.com/auth/devstorage.read_write https://www.googleapis.com/auth/identitytoolkit";

    private readonly HttpClient _http;
    private readonly FirebaseSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1); // only one request fetches a new token at a time
    private string? _token;                            // the token we already have (if any)
    private DateTime _expiresUtc;                      // when that token stops working

    public GoogleAccessTokenProvider(HttpClient http, FirebaseSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // The Firebase emulators accept the fixed admin token "owner" and need no key file.
        if (_settings.UseEmulators) return "owner";
        // Still have a token with more than 5 minutes left? Reuse it.
        if (_token is not null && DateTime.UtcNow < _expiresUtc - TimeSpan.FromMinutes(5)) return _token;
        await _gate.WaitAsync(ct);
        try
        {
            // check again: another request may have fetched a token while we were waiting
            if (_token is not null && DateTime.UtcNow < _expiresUtc - TimeSpan.FromMinutes(5)) return _token;

            // 1. Read the key file (downloaded from Firebase, kept outside the repository).
            JsonNode? key;
            try { key = JsonNode.Parse(await File.ReadAllTextAsync(_settings.ServiceAccountKeyPath, ct)); }
            catch (Exception e) { throw new RealtimeDatabaseException($"Could not read the service-account key file at {_settings.ServiceAccountKeyPath}.", e); }

            var email = key?["client_email"]?.GetValue<string>();     // who we are
            var privateKey = key?["private_key"]?.GetValue<string>(); // what we sign with
            var tokenUri = key?["token_uri"]?.GetValue<string>() ?? "https://oauth2.googleapis.com/token";
            if (email is null || privateKey is null) throw new RealtimeDatabaseException("The service-account key file is missing client_email or private_key.");

            // 2. Build a JWT: a header and claims (who, what for, valid for one hour).
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = email, scope = Scopes, aud = tokenUri, iat = now, exp = now + 3600 }));
            var unsigned = header + "." + claims;

            // 3. Sign it with the private key.
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            var signature = Base64Url(rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            // 4. Send the signed JWT to Google and get an access token back.
            using var response = await _http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = unsigned + "." + signature
            }), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new RealtimeDatabaseException($"Google refused the access token request ({(int)response.StatusCode}): {body}");

            // 5. Keep the token and when it expires (usually one hour).
            var json = JsonNode.Parse(body)!;
            _token = json["access_token"]!.GetValue<string>();
            _expiresUtc = DateTime.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3600);
            return _token;
        }
        finally
        {
            _gate.Release(); // always let the next request in, even after an error
        }
    }

    // JWTs use "base64url": normal base64 without "=" padding, with - and _ instead of + and /
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}