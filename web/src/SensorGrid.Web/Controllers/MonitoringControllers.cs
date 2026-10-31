// What this file is for: the monitoring pages: Dashboard (W2), Sites (W3), Site detail (W4), Sensors (W5), Register sensor (W6) and Readings log (W7).
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

    public async Task<IActionResult> Details(string id) => View(await _sites.GetDetailAsync(Me, id));

    /// <summary>Add site (administrators only): the dialog on W3.</summary>
    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Add(SiteForm form)
    {
        var result = await _sites.AddAsync(Me, form);
        if (result.Success) TempData["Flash"] = $"{form.Name?.Trim()} was added. Review its threshold bands, then register its sensors.";
        else TempData["FlashBad"] = string.Join(" ", result.Errors.Values);
        return RedirectToAction(nameof(Index));
    }
}

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
}

/// <summary>The form values plus the validation result, for the Register sensor view.</summary>
public sealed record RegisterPageModel(RegisterSensorForm Form, RegisterResult? Result, IReadOnlyList<(string SiteId, string Name)> Provinces);

/// <summary>W7 Readings log with filters, paging and CSV export.</summary>
public class ReadingsController : AppController
{
    private readonly IReadingService _readings;

    public ReadingsController(IReadingService readings) => _readings = readings;

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? site, string? sensor, int page = 1)
    {
        var d = _readings.DefaultFilter();
        var filter = new ReadingFilter(from ?? d.From, to ?? d.To, site, sensor, page);
        return View(await _readings.GetPageAsync(Me, filter));
    }

    /// <summary>Exports the whole filtered set (not only the visible page).</summary>
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, string? site, string? sensor, string? range)
    {
        var filter = range is not null ? _readings.FilterForRange(range, site) : new ReadingFilter(from, to, site, sensor, 1);
        return File(await _readings.ExportCsvAsync(Me, filter), "text/csv", "readings.csv");
    }
}
