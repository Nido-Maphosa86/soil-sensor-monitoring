// What this file is for: small classes that carry form values between a page and its controller (view models).
// How it fits in: Web project. Larger page data comes straight from the Service layer DTOs.
using System.ComponentModel.DataAnnotations;

namespace SensorGrid.Web.ViewModels;

/// <summary>Values of the sign in form.</summary>
public class LoginViewModel
{
    [Required, StringLength(200)] public string? Email { get; set; }
    [Required, StringLength(200)] public string? Password { get; set; }
    public bool KeepSignedIn { get; set; }
    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
}

/// <summary>Value of the forgot password form.</summary>
public class ForgotViewModel
{
    [StringLength(200)] public string? Email { get; set; }
}
