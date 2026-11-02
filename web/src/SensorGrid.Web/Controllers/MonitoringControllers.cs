// What this file is for: the monitoring pages: Dashboard (W2), Sites (W3), Site detail (W4), Sensors (W5), Register sensor (W6) and Readings log (W7),
// plus Edit site and Edit sensor (administrators only: edit, retire or reactivate a sensor, and delete a retired sensor or an empty site).
// How it fits in: Web project. Each action calls one service interface and hands the result to a view. Controllers never touch a repository or the database store.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorGrid.Service;
using SensorGrid.Web.Helpers;

namespace SensorGrid.Web.Controllers;

/// <summary>Base for pages: gives easy access to the signed in person.</summary>
public abstract class AppController : Controller
{
    protected UserContext Me => User.ToContext();

    /// <summary>True when a dialog sent its form in the background (site.js, data-ajax), so the answer is JSON instead of a new page.</summary>
    protected bool FromDialog => Request.Headers["X-Requested-With"] == "fetch";

    /// <summary>
    /// The answer to a dialog form. With errors, the dialog gets the message for each field ("" = the whole form) and stays open.
    /// Without errors, the green message is kept for the next page and the browser goes there.
    /// Without JavaScript the form is posted normally, so the errors fall back to the red banner on the next page.
    /// </summary>
    protected IActionResult DialogResult(IReadOnlyDictionary<string, string> errors, string okMessage, string nextUrl)
    {
        if (errors.Count == 0) TempData["Flash"] = okMessage;
        if (FromDialog) return errors.Count == 0 ? Json(new { ok = true, redirect = nextUrl }) : Json(new { ok = false, errors });
        if (errors.Count > 0) TempData["FlashBad"] = string.Join(" ", errors.Values);
        return Redirect(nextUrl);
    }
}

/// <summary>W2 Dashboard and the data for its trend chart.</summary>
public class DashboardController : AppController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index() => View(await _dashboard.GetAsync(Me));

    /// <summary>JSON used by the trend chart to switch range (last 15 minutes, 24 hours, 7 days, 30 days) and value without reloading the page.</summary>
    [HttpGet]
    public async Task<IActionResult> Trend(string? range, string? parameter) => Json(await _dashboard.GetTrendChartAsync(Me, range, parameter));
}

/// <summary>W3 Sites and W4 Site detail.</summary>
public class SitesController : AppController
{
    private readonly ISiteService _sites;

    public SitesController(ISiteService sites) => _sites = sites;

    public async Task<IActionResult> Index() => View(await _sites.GetSitesAsync(Me));

    /// <summary>W4. sensor (optional) shows one sensor's live readings instead of the average.</summary>
    public async Task<IActionResult> Details(string id, string? sensor) => View(await _sites.GetDetailAsync(Me, id, sensor));

    /// <summary>Add site (administrators only): the dialog on W3.</summary>
    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Add(SiteForm form)
    {
        var result = await _sites.AddAsync(Me, form);
        return DialogResult(result.Errors, $"{form.Name?.Trim()} was added. Review its threshold bands, then register its sensors.", Url.Action(nameof(Index))!);
    }

    /// <summary>Edit site (administrators only): the details form, and Delete once the site has no sensors.</summary>
    [HttpGet, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Edit(string id) => View(new SiteEditPageModel(await _sites.GetEditAsync(Me, id), null));

    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Edit(string id, SiteForm form)
    {
        var result = await _sites.UpdateAsync(Me, id, form);
        if (!result.Success)
        {
            var page = await _sites.GetEditAsync(Me, id);
            return View(new SiteEditPageModel(page with { Form = form }, result));
        }
        TempData["Flash"] = $"{form.Name?.Trim()} was saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Delete site: the dialog on Edit site. The code must be typed to confirm.</summary>
    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id, string? confirm)
    {
        var result = await _sites.DeleteAsync(Me, id, confirm);
        return DialogResult(result.Errors, $"Site {id} was deleted.", result.Success ? Url.Action(nameof(Index))! : Url.Action(nameof(Edit), new { id })!);
    }
}

/// <summary>The Edit site page, plus the errors of a save that was refused.</summary>
public sealed record SiteEditPageModel(SiteEditDto Page, ChangeResult? Result);

/// <summary>The Edit sensor page, plus the errors of a save that was refused.</summary>
public sealed record SensorEditPageModel(SensorEditDto Page, ChangeResult? Result);

/// <summary>W5 Sensors and W6 Register sensor (administrators only).</summary>
public class SensorsController : AppController
{
    private readonly ISensorService _sensors;

    public SensorsController(ISensorService sensors) => _sensors = sensors;

    public async Task<IActionResult> Index(string? site, string? search, string? state, int page = 1) =>
        View(await _sensors.GetPageAsync(Me, site, search, state, page));

    [Authorize(Policy = "AdminOnly"), HttpGet]
    public async Task<IActionResult> Register(string? site)
    {
        var provinces = await _sensors.ProvincesAsync(Me);
        if (provinces.Count == 0)
        {
            TempData["FlashBad"] = "Add a site first. Sensors are registered to a site.";
            return RedirectToAction("Index", "Sites");
        }
        return View(new RegisterPageModel(await _sensors.NewFormAsync(Me, site), null, provinces));
    }

    [Authorize(Policy = "AdminOnly"), HttpPost]
    public async Task<IActionResult> Register(RegisterSensorForm form)
    {
        var result = await _sensors.RegisterAsync(Me, form);
        if (result.Success)
        {
            TempData["Flash"] = $"Sensor {form.SensorId!.Trim().ToUpperInvariant()} was registered.";
            return RedirectToAction(nameof(Index), new { site = form.SiteId });
        }
        return View(new RegisterPageModel(form, result, await _sensors.ProvincesAsync(Me)));
    }

    /// <summary>Edit sensor (administrators only): its details, Retire or Reactivate, and Delete once retired.</summary>
    [Authorize(Policy = "AdminOnly"), HttpGet]
    public async Task<IActionResult> Edit(string id) => View(new SensorEditPageModel(await _sensors.GetEditAsync(Me, id), null));

    [Authorize(Policy = "AdminOnly"), HttpPost]
    public async Task<IActionResult> Edit(string id, RegisterSensorForm form)
    {
        form.SensorId = id; // the code comes from the address, never from the form
        var result = await _sensors.UpdateAsync(Me, form);
        if (!result.Success)
        {
            var page = await _sensors.GetEditAsync(Me, id);
            return View(new SensorEditPageModel(page with { Form = form }, result));
        }
        TempData["Flash"] = $"Sensor {id} was saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Policy = "AdminOnly"), HttpPost]
    public async Task<IActionResult> Retire(string id)
    {
        await _sensors.SetActiveAsync(Me, id, false);
        TempData["Flash"] = $"Sensor {id} was retired. Its readings are kept.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Policy = "AdminOnly"), HttpPost]
    public async Task<IActionResult> Reactivate(string id)
    {
        await _sensors.SetActiveAsync(Me, id, true);
        TempData["Flash"] = $"Sensor {id} is active again.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Delete sensor: the dialog on Edit sensor. Only a retired sensor, and the code must be typed to confirm.</summary>
    [Authorize(Policy = "AdminOnly"), HttpPost]
    public async Task<IActionResult> Delete(string id, string? confirm)
    {
        var result = await _sensors.DeleteAsync(Me, id, confirm);
        return DialogResult(result.Errors, $"Sensor {id} was deleted. Its readings are kept.", result.Success ? Url.Action(nameof(Index))! : Url.Action(nameof(Edit), new { id })!);
    }
}

/// <summary>The form values plus the validation result, for the Register sensor view.</summary>
public sealed record RegisterPageModel(RegisterSensorForm Form, RegisterResult? Result, IReadOnlyList<(string SiteId, string Name)> Provinces);

/// <summary>W7 Readings log with filters, paging and CSV export.</summary>
public class ReadingsController : AppController
{
    private readonly IReadingService _readings;

    public ReadingsController(IReadingService readings) => _readings = readings;

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? site, string? sensor, string? before, string? after)
    {
        var d = _readings.DefaultFilter();
        var filter = new ReadingFilter(from ?? d.From, to ?? d.To, site, sensor, before, after);
        return View(await _readings.GetPageAsync(Me, filter, TempData["ExportError"] as string));
    }

    /// <summary>Exports the whole filtered set (not only the visible page). A period that is too long goes back to the log with the reason under the dates.</summary>
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, string? site, string? sensor, string? range)
    {
        var filter = range is not null ? _readings.FilterForRange(range, site) : new ReadingFilter(from, to, site, sensor);
        var export = await _readings.ExportCsvAsync(Me, filter);
        if (export.Csv is not null) return File(export.Csv, "text/csv", "readings.csv");
        TempData["ExportError"] = export.Error;
        return RedirectToAction(nameof(Index), new { from = filter.From?.ToString("yyyy-MM-ddTHH:mm"), to = filter.To?.ToString("yyyy-MM-ddTHH:mm"), site, sensor });
    }
}
