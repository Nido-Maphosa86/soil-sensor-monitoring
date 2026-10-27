// What this file is for: the "Waiting for the Firebase connection" page, shown for every address while a Firebase setting is missing.
// How it fits in: Web project. Program.cs sends every request here until the settings are complete. It lists the names of the missing settings
// (never their values) and the steps in README.md.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorGrid.Repository;

namespace SensorGrid.Web.Controllers;

/// <summary>Shows which Firebase settings are still missing. Once connected, the page simply sends people to sign in.</summary>
[AllowAnonymous]
public class SetupController : Controller
{
    private readonly ConnectionState _connection;

    public SetupController(ConnectionState connection) => _connection = connection;

    [HttpGet, IgnoreAntiforgeryToken]
    public IActionResult Index()
    {
        if (_connection.IsConnected) return RedirectToAction("Login", "Account");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View(_connection);
    }
}
