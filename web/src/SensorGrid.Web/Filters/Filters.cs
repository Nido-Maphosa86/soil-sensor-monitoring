// What this file is for: two filters that run around every controller action: one fills the top bar data, one turns "not allowed / not found / database down" errors into proper pages.
// How it fits in: Web project. Registered once in Program.cs so no controller has to repeat this work.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SensorGrid.Repository;
using SensorGrid.Service;
using SensorGrid.Web.Helpers;

namespace SensorGrid.Web.Filters;

/// <summary>Puts the signed in person's name, unread count and open alert count into ViewData["Shell"] for the layout.</summary>
public sealed class ShellFilter : IAsyncResultFilter
{
    private readonly IShellService _shell;

    public ShellFilter(IShellService shell) => _shell = shell;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult view && context.HttpContext.User.Identity?.IsAuthenticated == true)
            view.ViewData["Shell"] = await _shell.GetAsync(context.HttpContext.User.ToContext());
        await next();
    }
}

/// <summary>Maps rule errors: Forbidden gives the access denied page, NotFound gives a 404 page, a database problem gives a 503 page.</summary>
public sealed class ErrorFilter : IExceptionFilter
{
    private readonly ILogger<ErrorFilter> _log;

    public ErrorFilter(ILogger<ErrorFilter> log) => _log = log;

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ForbiddenException:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                context.ExceptionHandled = true;
                break;
            case NotFoundException e:
                context.Result = Page(context, e.Message, 404);
                context.ExceptionHandled = true;
                break;
            case RealtimeDatabaseException or AuthGatewayException or HttpRequestException or TaskCanceledException:
                // The details (path, response) go to the log, not to the screen.
                _log.LogError(context.Exception, "Firebase problem");
                context.Result = Page(context, "Firebase could not be reached. Please try again in a moment.", 503);
                context.ExceptionHandled = true;
                break;
        }
    }

    private static ViewResult Page(ExceptionContext context, string message, int status) => new()
    {
        ViewName = "NotFound",
        ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState) { ["Message"] = message },
        StatusCode = status
    };
}
