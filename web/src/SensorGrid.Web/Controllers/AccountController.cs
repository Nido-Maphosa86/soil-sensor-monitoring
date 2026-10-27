// What this file is for: sign in (W1), forgot password (W1b), sign out, the access denied page and the deactivated-account page.
// How it fits in: Web project. It only talks to IAuthService; the cookie is created here with Role and province claims.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorGrid.Service;
using SensorGrid.Web.Helpers;
using SensorGrid.Web.ViewModels;

namespace SensorGrid.Web.Controllers;

/// <summary>Sign in, forgot password and sign out.</summary>
public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var result = await _auth.SignInAsync(model.Email, model.Password);
        if (result.Outcome == SignInOutcome.Denied) return View("Deactivated"); // a deactivated account sees "Access denied"
        if (result.Outcome == SignInOutcome.Unavailable)
        {
            model.Password = null;
            model.Error = "Sign in is not available right now. Please try again in a few minutes.";
            return View(model);
        }
        if (result.Outcome != SignInOutcome.Success || result.User is null)
        {
            // one message for every kind of failure, so it never reveals which emails exist
            model.Password = null;
            model.Error = "The email or password is not correct.";
            return View(model);
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ClaimsFactory.ToPrincipal(result.User),
            new AuthenticationProperties { IsPersistent = model.KeepSignedIn });
        // only local addresses are accepted, so a crafted link cannot send the person to another website
        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Forgot() => View(new ForgotViewModel());

    /// <summary>Always shows the same confirmation, whether or not the email belongs to an account.</summary>
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Forgot(ForgotViewModel model)
    {
        await _auth.RequestPasswordResetAsync(model.Email);
        return View("ForgotSent");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
