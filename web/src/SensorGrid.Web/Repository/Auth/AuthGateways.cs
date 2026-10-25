// What this file is for: sign-in, password emails, invitations, and enabling or disabling accounts, through Firebase Authentication.
// How it fits in: Repository folder. Firebase holds the passwords; our database never does. After sign-in the Service layer reads users/{uid}
// for the role and the account status. The same Firebase accounts sign in to the mobile app (field officers only, see firebase/database.rules.json).
// CONNECT FIREBASE HERE: needs SensorGrid:Firebase:WebApiKey (sign in, password emails) and the service-account token (create, look up, update and
// disable accounts). In the Firebase console: Authentication > Sign-in method > enable Email/Password.
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace SensorGrid.Repository;

/// <summary>Wrong email or password. The message never says which part was wrong.</summary>
public class AuthFailedException : Exception
{
    public AuthFailedException() : base("The email or password is not correct.") { }
}

/// <summary>The account is disabled at Firebase Authentication (it was deactivated).</summary>
public class AccountDisabledException : Exception
{
    public AccountDisabledException() : base("This account is deactivated.") { }
}

/// <summary>Firebase Authentication refused a request (for example the email already has an account). The message is safe to show to an administrator.</summary>
public class AuthGatewayException : Exception
{
    public AuthGatewayException(string message) : base(message) { }
}

public interface IAuthGateway
{
    /// <summary>Checks the email and password and returns the user id. Throws AuthFailedException or AccountDisabledException.</summary>
    Task<string> SignInAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Sends the "reset your password" email. Finishes the same way whether or not the email has an account.</summary>
    Task SendPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Sends the invitation: Firebase's "set your password" email. After setting it, the person signs in on the web,
    /// and field officers also on the mobile app. Throws AuthGatewayException when the email could not be sent.
    /// </summary>
    Task SendInvitationAsync(string email, CancellationToken ct = default);

    /// <summary>Creates a sign-in account with a long random password nobody knows, and returns its user id. The invitation email lets the person choose their own.</summary>
    Task<string> CreateAccountAsync(string email, string displayName, CancellationToken ct = default);

    /// <summary>Finds the user id of an existing sign-in account by email, or null.</summary>
    Task<string?> FindUserIdAsync(string email, CancellationToken ct = default);

    /// <summary>Changes the sign-in email and display name of an account (an administrator edited the account).</summary>
    Task UpdateAccountAsync(string uid, string email, string displayName, CancellationToken ct = default);

    /// <summary>Changes a password after checking the current one. Throws AuthFailedException when the current password is wrong.</summary>
    Task ChangePasswordAsync(string email, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Disables or enables sign in for an account (deactivated or reactivated). A disabled account cannot sign in on the web or the mobile app.</summary>
    Task SetDisabledAsync(string uid, bool disabled, CancellationToken ct = default);
}

/// <summary>The Firebase Authentication REST API (Identity Toolkit), or its emulator when SensorGrid:Firebase:UseEmulators is true.</summary>
public sealed class FirebaseAuthGateway : IAuthGateway
{
    private readonly HttpClient _http;
    private readonly FirebaseSettings _settings;
    private readonly SensorGridSettings _app;
    private readonly IAccessTokenProvider _tokens;

    public FirebaseAuthGateway(HttpClient http, FirebaseSettings settings, SensorGridSettings app, IAccessTokenProvider tokens)
    {
        _http = http;
        _settings = settings;
        _app = app;
        _tokens = tokens;
    }

    private string Root => _settings.AuthRoot;

    public async Task<string> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        var json = await SignInRawAsync(email, password, ct);
        return json["localId"]!.GetValue<string>();
    }

    public async Task SendPasswordResetAsync(string email, CancellationToken ct = default)
    {
        try
        {
            await SendOobAsync(email, ct);
        }
        catch (AuthGatewayException e) when (e.Message.Contains("EMAIL_NOT_FOUND", StringComparison.Ordinal))
        {
            // never reveal which emails have accounts
        }
    }

    public Task SendInvitationAsync(string email, CancellationToken ct = default) => SendOobAsync(email, ct);

    /// <summary>
    /// Asks Firebase to email a password link. Firebase writes the email from the template in the console (Authentication > Templates > Password reset);
    /// when SensorGrid:WebAppUrl is set, the email's "continue" link brings the person back to the sign in page.
    /// </summary>
    private async Task SendOobAsync(string email, CancellationToken ct)
    {
        var body = new JsonObject { ["requestType"] = "PASSWORD_RESET", ["email"] = email };
        if (!string.IsNullOrWhiteSpace(_app.WebAppUrl)) body["continueUrl"] = _app.WebAppUrl.TrimEnd('/') + "/Account/Login";
        using var response = await _http.PostAsJsonAsync($"{Root}/accounts:sendOobCode?key={_settings.WebApiKey}", body, ct);
        if (!response.IsSuccessStatusCode) throw new AuthGatewayException($"Firebase could not send the email ({await ErrorMessage(response, ct)}).");
    }

    public async Task<string> CreateAccountAsync(string email, string displayName, CancellationToken ct = default)
    {
        var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // replaced by the person through the invitation email
        var json = await AdminAsync($"projects/{_settings.ProjectId}/accounts",
            new JsonObject { ["email"] = email, ["displayName"] = displayName, ["password"] = randomPassword, ["emailVerified"] = false }, "create the account", ct);
        return json["localId"]!.GetValue<string>();
    }

    public async Task<string?> FindUserIdAsync(string email, CancellationToken ct = default)
    {
        var json = await AdminAsync($"projects/{_settings.ProjectId}/accounts:lookup", new JsonObject { ["email"] = new JsonArray(email) }, "look up the account", ct);
        return json["users"] is JsonArray { Count: > 0 } users ? users[0]?["localId"]?.GetValue<string>() : null;
    }

    public Task UpdateAccountAsync(string uid, string email, string displayName, CancellationToken ct = default) =>
        AdminAsync($"projects/{_settings.ProjectId}/accounts:update",
            new JsonObject { ["localId"] = uid, ["email"] = email, ["displayName"] = displayName }, "update the account", ct);

    public async Task ChangePasswordAsync(string email, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        // Signing in again proves the current password; the fresh token then allows the change.
        var idToken = (await SignInRawAsync(email, currentPassword, ct))["idToken"]!.GetValue<string>();
        using var response = await _http.PostAsJsonAsync($"{Root}/accounts:update?key={_settings.WebApiKey}", new { idToken, password = newPassword, returnSecureToken = false }, ct);
        if (!response.IsSuccessStatusCode) throw new AuthGatewayException($"Firebase could not change the password ({await ErrorMessage(response, ct)}).");
    }

    public Task SetDisabledAsync(string uid, bool disabled, CancellationToken ct = default) =>
        AdminAsync($"projects/{_settings.ProjectId}/accounts:update", new JsonObject { ["localId"] = uid, ["disableUser"] = disabled },
            disabled ? "disable the account" : "enable the account", ct);

    /// <summary>An administrator call, signed with the service-account token. Throws AuthGatewayException with Firebase's reason when it fails.</summary>
    private async Task<JsonNode> AdminAsync(string relative, JsonObject body, string what, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/{relative}") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new("Bearer", await _tokens.GetAccessTokenAsync(ct));
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var reason = await ErrorMessage(response, ct);
            throw new AuthGatewayException(reason.StartsWith("EMAIL_EXISTS", StringComparison.Ordinal) || reason.StartsWith("DUPLICATE_EMAIL", StringComparison.Ordinal)
                ? "Another sign-in account already uses this email."
                : $"Firebase could not {what} ({reason}).");
        }
        var text = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
    }

    private async Task<JsonNode> SignInRawAsync(string email, string password, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"{Root}/accounts:signInWithPassword?key={_settings.WebApiKey}", new { email, password, returnSecureToken = true }, ct);
        if (response.IsSuccessStatusCode) return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        var message = await ErrorMessage(response, ct);
        if (message.StartsWith("USER_DISABLED", StringComparison.Ordinal)) throw new AccountDisabledException();
        // EMAIL_NOT_FOUND, INVALID_PASSWORD, INVALID_LOGIN_CREDENTIALS and the rest all give the same answer.
        throw new AuthFailedException();
    }

    private static async Task<string> ErrorMessage(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        try { return JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>() ?? $"HTTP {(int)response.StatusCode}"; }
        catch { return $"HTTP {(int)response.StatusCode}"; }
    }
}
