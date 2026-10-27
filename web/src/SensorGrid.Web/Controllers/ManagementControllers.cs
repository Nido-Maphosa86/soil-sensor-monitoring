// What this file is for: the management pages: Thresholds (W8), Alerts (W9), Reports (W10), User accounts (W11), Profile (W12/W12b) and the bell panel data.
// How it fits in: Web project. Admin-only pages carry [Authorize(Policy = "AdminOnly")], so the server refuses the URL, not only the menu link.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorGrid.Domain;
using SensorGrid.Service;

namespace SensorGrid.Web.Controllers;

/// <summary>W8 Thresholds. Administrators only.</summary>
[Authorize(Policy = "AdminOnly")]
public class ThresholdsController : AppController
{
    private readonly IThresholdService _thresholds;

    public ThresholdsController(IThresholdService thresholds) => _thresholds = thresholds;

    public async Task<IActionResult> Index(string? site)
    {
        var id = site ?? await _thresholds.DefaultSiteIdAsync(Me);
        if (id.Length == 0)
        {
            TempData["FlashBad"] = "Add a site first. Threshold bands are set per site.";
            return RedirectToAction("Index", "Sites");
        }
        return View(await _thresholds.GetEditorAsync(Me, id));
    }

    [HttpPost]
    public async Task<IActionResult> Save(string site, string? basis)
    {
        var input = ParameterCatalog.Readings.ToDictionary(i => i.Parameter,
            i => ((string?)Request.Form[$"min_{i.Parameter}"].FirstOrDefault(), (string?)Request.Form[$"max_{i.Parameter}"].FirstOrDefault()));
        var result = await _thresholds.SaveAsync(Me, site, basis, input);
        if (result.Success)
        {
            TempData["Flash"] = "Threshold bands saved.";
            return RedirectToAction(nameof(Index), new { site });
        }
        TempData["FlashBad"] = "Some values were not saved. Fix the marked fields and try again.";
        return View("Index", result.Editor);
    }

    [HttpPost]
    public async Task<IActionResult> Reset(string site)
    {
        await _thresholds.ResetToDefaultsAsync(Me, site);
        TempData["Flash"] = "Bands were reset to the defaults.";
        return RedirectToAction(nameof(Index), new { site });
    }
}

/// <summary>W9 Alerts: list, acknowledge, resolve, view note, export.</summary>
public class AlertsController : AppController
{
    private readonly IAlertService _alerts;

    public AlertsController(IAlertService alerts) => _alerts = alerts;

    public async Task<IActionResult> Index(string? chip, string? q, string? site) => View(new AlertsViewModel(await _alerts.GetPageAsync(Me, chip, q, site), chip ?? "All", q, site));

    [HttpPost]
    public Task<IActionResult> Acknowledge(string id, string? note, string? chip) => ActAsync(() => _alerts.AcknowledgeAsync(Me, id, note), "Alert acknowledged.", chip);

    [HttpPost]
    public Task<IActionResult> Resolve(string id, string? note, string? chip) => ActAsync(() => _alerts.ResolveAsync(Me, id, note), "Alert resolved.", chip);

    /// <summary>Runs the change; a broken rule (for example a missing note) is shown to the person and nothing is saved.</summary>
    private async Task<IActionResult> ActAsync(Func<Task> change, string okMessage, string? chip)
    {
        try
        {
            await change();
            TempData["Flash"] = okMessage;
        }
        catch (DomainException e)
        {
            TempData["FlashBad"] = e.Message;
        }
        return RedirectToAction(nameof(Index), new { chip });
    }

    [HttpGet]
    public async Task<IActionResult> Note(string id) => Json(await _alerts.GetNoteAsync(Me, id));

    public async Task<IActionResult> Export() => File(await _alerts.ExportCsvAsync(Me), "text/csv", "alerts.csv");
}

/// <summary>Page model for the Alerts view: the data plus the current filters.</summary>
public sealed record AlertsViewModel(AlertsPageDto Page, string Chip, string? Query, string? Site);

/// <summary>W10 Reports.</summary>
public class ReportsController : AppController
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index(string? period) => View(await _reports.GetPageAsync(Me, period));

    [HttpPost]
    public async Task<IActionResult> Generate(ReportKind kind, ReportFormat format, string? period)
    {
        var result = await _reports.GenerateAsync(Me, kind, format, period);
        if (result.Success) TempData["Flash"] = "Report generated. You can download it below.";
        else TempData["FlashBad"] = result.Error;
        return RedirectToAction(nameof(Index), new { period });
    }

    public async Task<IActionResult> Download(string id)
    {
        var file = await _reports.DownloadAsync(Me, id);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

/// <summary>W11 User accounts. Administrators only.</summary>
[Authorize(Policy = "AdminOnly")]
public class UsersController : AppController
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    public async Task<IActionResult> Index(string? q) => View(await _users.GetPageAsync(Me, q));

    /// <summary>Invite user: creates the Firebase account and Firebase emails the person a link to set their password.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(UserForm form)
    {
        var result = await _users.CreateAsync(Me, form);
        Report(result, $"Invitation sent to {form.Email?.Trim()}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Resend(string id)
    {
        Report(await _users.ResendInvitationAsync(Me, id), "Invitation sent again.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserForm form)
    {
        var result = await _users.UpdateAsync(Me, form);
        Report(result, "Account updated.");
        return RedirectToAction(nameof(Index));
    }

    private void Report(UserSaveResult result, string ok)
    {
        if (result.Success) TempData["Flash"] = result.Notice ?? ok;
        else TempData["FlashBad"] = string.Join(" ", result.Errors.Values);
    }
}

/// <summary>W12 Profile and W12b Change password dialog.</summary>
public class ProfileController : AppController
{
    private readonly IProfileService _profile;
    private readonly IAuthService _auth;

    public ProfileController(IProfileService profile, IAuthService auth)
    {
        _profile = profile;
        _auth = auth;
    }

    public async Task<IActionResult> Index() => View(await _profile.GetAsync(Me));

    [HttpPost]
    public async Task<IActionResult> Notifications(bool breach, bool offline, bool daily)
    {
        await _profile.SaveNotificationsAsync(Me, breach, offline, daily);
        TempData["Flash"] = "Notification choices saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string? current, string? next, string? confirm)
    {
        var errors = await _auth.ChangePasswordAsync(Me.UserId, current, next, confirm);
        if (errors.Count == 0) TempData["Flash"] = "Password updated.";
        else
        {
            TempData["FlashBad"] = string.Join(" ", errors);
            TempData["OpenPasswordDialog"] = "1";
        }
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>Data for the bell panel in the top bar. A notification is identified by its alert id.</summary>
public class NotificationsController : AppController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> List() => Json(await _notifications.GetBellAsync(Me));

    [HttpPost]
    public async Task<IActionResult> Read(string id)
    {
        await _notifications.MarkReadAsync(Me, id);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> ReadAll()
    {
        await _notifications.MarkAllReadAsync(Me);
        return NoContent();
    }
}

/// <summary>Shows the error page for unexpected problems.</summary>
[AllowAnonymous]
public class ErrorController : Controller
{
    [Route("Error")]
    public IActionResult Index()
    {
        Response.StatusCode = 500;
        return View("~/Views/Shared/Error.cshtml");
    }
}
