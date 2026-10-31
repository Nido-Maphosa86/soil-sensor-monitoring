// What this file is for: sign in, change password, forgot password, and the small "shell" data (name, open alert count, unread count) every page needs.
// How it fits in: Service folder. The Web project calls these through IAuthService and IShellService.
// Sign in goes through IAuthGateway (Firebase Authentication), then users/{uid} gives the role and account status.
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public sealed class AuthService : IAuthService
{
    private readonly IAuthGateway _gateway;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public AuthService(IAuthGateway gateway, IUserRepository users, IClock clock)
    {
        _gateway = gateway;
        _users = users;
        _clock = clock;
    }

    /// <summary>Builds the request context from a stored user.</summary>
    public static UserContext ToContext(AppUser user) => new(user.UserId, user.Name, user.Role, user.SiteIds.ToList());

    public async Task<SignInResult> SignInAsync(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return new SignInResult(SignInOutcome.Failed, null);

        string uid;
        try
        {
            uid = await _gateway.SignInAsync(email.Trim(), password);
        }
        catch (AuthFailedException) { return new SignInResult(SignInOutcome.Failed, null); }       // one answer for any wrong detail
        catch (AccountDisabledException) { return new SignInResult(SignInOutcome.Denied, null); }  // disabled at Firebase: deactivated
        catch (Exception e) when (e is AuthGatewayException or HttpRequestException or TaskCanceledException) { return new SignInResult(SignInOutcome.Unavailable, null); }

        AppUser? user;
        try { user = await _users.GetAsync(uid); }
        catch (RealtimeDatabaseException) { return new SignInResult(SignInOutcome.Unavailable, null); }
        if (user is null) return new SignInResult(SignInOutcome.Failed, null); // has a sign-in but no users/{uid} record
        if (user.Status == AccountStatus.Deactivated) return new SignInResult(SignInOutcome.Denied, null);

        user.LastActiveAt = _clock.UtcNow;
        if (user.Status == AccountStatus.Invited)
        {
            user.Status = AccountStatus.Active; // the first sign in finishes the invitation
            await _users.SaveAsync(user);
        }
        else
        {
            await _users.TouchLastActiveAsync(uid, _clock.UtcNow);
        }
        return new SignInResult(SignInOutcome.Success, ToContext(user));
    }

    public async Task<UserContext?> RefreshAsync(string userId)
    {
        var user = await _users.GetAsync(userId);
        return user is { Status: AccountStatus.Active } ? ToContext(user) : null;
    }

    public async Task<IReadOnlyList<string>> ChangePasswordAsync(string userId, string? current, string? next, string? confirm)
    {
        var errors = new List<string>();
        var user = await _users.GetAsync(userId);
        if (user is null) return new[] { "Account not found." };
        if (string.IsNullOrEmpty(current)) errors.Add("The current password is not correct.");
        if (!PasswordRule.IsStrong(next))
            errors.Add("The new password is too weak. " + PasswordRule.Description);
        if (next != confirm) errors.Add("The new password and the confirmation do not match.");
        if (errors.Count > 0) return errors;

        try
        {
            await _gateway.ChangePasswordAsync(user.Email, current!, next!);
        }
        catch (AuthFailedException)
        {
            errors.Add("The current password is not correct.");
        }
        catch (Exception e) when (e is AuthGatewayException or HttpRequestException or TaskCanceledException)
        {
            errors.Add("The password could not be changed right now. Please try again in a few minutes.");
        }
        return errors;
    }

    public async Task RequestPasswordResetAsync(string? email)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(email)) await _gateway.SendPasswordResetAsync(email.Trim());
        }
        catch
        {
            // Deliberately ignored: the screen shows the same message whatever happens, so nothing reveals which emails have accounts.
        }
    }
}

public sealed class ShellService : IShellService
{
    private readonly IUserRepository _users;
    private readonly IAlertRepository _alerts;
    private readonly INotificationRepository _notifications;
    private readonly DataScope _scope;

    public ShellService(IUserRepository users, IAlertRepository alerts, INotificationRepository notifications, DataScope scope)
    {
        _users = users;
        _alerts = alerts;
        _notifications = notifications;
        _scope = scope;
    }

    public async Task<ShellDto> GetAsync(UserContext ctx)
    {
        // the account, the notifications and the alerts do not depend on each other, so they are asked for at the same time
        var userTask = _users.GetAsync(ctx.UserId);
        var notificationsTask = _notifications.ForUserAsync(ctx.UserId);
        var sites = (await _scope.VisibleSitesAsync(ctx)).Select(s => s.SiteId).ToList();
        var alerts = await _alerts.GetAllAsync(sites);
        var user = await userTask;
        var visibleAlertIds = alerts.Select(a => a.AlertId).ToHashSet();
        var unread = (await notificationsTask).Count(n => !n.IsRead && visibleAlertIds.Contains(n.AlertId));
        var name = user?.Name ?? ctx.Name;
        return new ShellDto(ctx.UserId, name, Display.Initials(name), user?.Email ?? "", Display.RoleLabel(ctx.Role), ctx.IsAdmin,
            alerts.Count(a => a.Status == AlertStatus.Open), unread);
    }
}
