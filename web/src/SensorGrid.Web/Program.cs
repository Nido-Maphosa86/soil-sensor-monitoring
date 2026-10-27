// What this file is for: starts the web application: wiring of the layers, sign-in cookie, security rules and routes.
// How it fits in: Web project entry point. All data and every account come from Firebase. While a Firebase setting is missing, the app still starts
// and every page shows the "Waiting for the Firebase connection" page, which lists the missing settings (see Views/Setup/Index.cshtml).
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using SensorGrid.Domain;
using SensorGrid.Repository;
using SensorGrid.Service;
using SensorGrid.Web.Filters;
using SensorGrid.Web.Helpers;

// Numbers use a dot and dates use English month names everywhere, whatever the computer's language is.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Display.Culture;
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Display.Culture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); // every POST needs an anti-forgery token
    options.Filters.Add(new AuthorizeFilter());                         // every page needs a signed in person unless marked AllowAnonymous
    options.Filters.Add<ShellFilter>();
    options.Filters.Add<ErrorFilter>();
});

// --- The layers. CONNECT FIREBASE HERE: AddRepositoryLayer reads the SensorGrid:Firebase:* settings (fill them with user-secrets, see README.md). ---
builder.Services.AddRepositoryLayer(builder.Configuration);
builder.Services.AddServiceLayer(builder.Configuration);

var settings = builder.Configuration.GetSection("SensorGrid").Get<SensorGridSettings>() ?? new SensorGridSettings();
var connected = settings.Firebase.Missing(settings.UseCloudStorageForReports).Count == 0;

// The evaluator runs inside the web app only when asked to (the fallback for the free Spark plan) and only once Firebase is connected.
if (connected && settings.RunAlertEvaluatorInWebApp)
    builder.Services.AddHostedService<AlertEvaluatorWorker>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "sensorgrid.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    // On every request, reload the account from users/{uid}: a deactivated person is signed out at once and role/province changes apply straight away.
    options.Events.OnValidatePrincipal = async context =>
    {
        var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        UserContext? fresh = null;
        if (connected && id is not null)
        {
            try { fresh = await context.HttpContext.RequestServices.GetRequiredService<IAuthService>().RefreshAsync(id); }
            catch (RealtimeDatabaseException) { fresh = null; } // database unreachable: sign out rather than trust an old cookie
        }
        if (fresh is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        context.ReplacePrincipal(ClaimsFactory.ToPrincipal(fresh));
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(nameof(UserRole.Administrator)));
});

var app = builder.Build();

if (connected) await app.Services.EnsureFirstAdministratorAsync(); // only does something while the database has no accounts
else app.Logger.LogWarning("Waiting for the Firebase connection. Missing: {Missing}", string.Join(", ", settings.Firebase.Missing(settings.UseCloudStorageForReports)));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// basic browser protections on every response
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

app.UseStaticFiles();

// Not connected yet: every address shows the waiting page (styles and scripts above are still served).
if (!connected)
{
    app.Use(async (context, next) =>
    {
        context.Request.Path = "/Setup";
        context.Request.Method = HttpMethods.Get;
        await next();
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Debug only (Development environment, signed in administrator): shows the stored JSON of one path, so the shape can be checked against the database design.
// Example: /debug/tree?path=alerts/GP
if (app.Environment.IsDevelopment() && connected)
{
    app.MapGet("/debug/tree", async (string? path, IRealtimeDatabaseStore store) =>
        Results.Content((await store.GetAsync(path ?? "") is { } node ? node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) : "null"), "application/json"))
        .RequireAuthorization("AdminOnly");
}

app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.Run();
