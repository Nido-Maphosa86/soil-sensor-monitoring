// What this file is for: small helpers for the web layer: claims (who is signed in), colours of status words, and the SensorGrid logo mark.
// How it fits in: Web project. Controllers use the claims helpers; views use Ui to print status words and colours exactly like the prototype.
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Html;
using SensorGrid.Domain;
using SensorGrid.Service;

namespace SensorGrid.Web.Helpers;

/// <summary>Turns a UserContext into a cookie identity (Role and province claims) and back.</summary>
public static class ClaimsFactory
{
    public const string SiteClaim = "site";

    public static ClaimsPrincipal ToPrincipal(UserContext ctx)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, ctx.UserId),
            new(ClaimTypes.Name, ctx.Name),
            new(ClaimTypes.Role, ctx.Role.ToString())
        };
        claims.AddRange(ctx.SiteIds.Select(id => new Claim(SiteClaim, id)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }

    /// <summary>Reads the signed in person from the cookie claims.</summary>
    public static UserContext ToContext(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
        user.FindFirstValue(ClaimTypes.Name) ?? "",
        Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role) ?? nameof(UserRole.StandardUser)),
        user.FindAll(SiteClaim).Select(c => c.Value).ToList());
}

/// <summary>Colours and small HTML pieces that repeat on many screens (same values as the prototype).</summary>
public static class Ui
{
    public static string Colour(Tone t) => t switch
    {
        Tone.Ok => "#10795A", Tone.Warn => "#B87A15", Tone.Bad => "#C0392F", Tone.Info => "#2F80C2", Tone.Violet => "#6C55C4", _ => "#8A9691"
    };

    /// <summary>The soft background that goes with a tone (icon tiles and pills).</summary>
    public static string Bg(Tone t) => t switch
    {
        Tone.Ok => "#E3F1EB", Tone.Warn => "#FBF0DC", Tone.Bad => "#FBE9E7", Tone.Info => "#E6F0F9", Tone.Violet => "#EEEAF9", _ => "#EEF1EF"
    };

    public static string Colour(AlertKindView k) => k switch { AlertKindView.Critical => "#C0392F", AlertKindView.Warning => "#B87A15", _ => "#8A9691" };

    /// <summary>A coloured dot followed by a word, like "● Online".</summary>
    public static IHtmlContent Status(string text, string colour) => new HtmlString(
        $"<span class=\"dotpill\" style=\"color:{colour}\"><s style=\"background:{colour}\"></s>{WebUtility.HtmlEncode(text)}</span>");

    public static IHtmlContent Status(string text, Tone tone) => Status(text, Colour(tone));

    /// <summary>The SensorGrid mark: a probe housing, three prongs and a row of grid nodes (same drawing as the prototype).</summary>
    public static IHtmlContent Mark(int size, int radius) => new HtmlString(
        $"<div style=\"width:{size}px;height:{size}px;border-radius:{radius}px;background:var(--mint);display:flex;align-items:center;justify-content:center;flex:0 0 auto\">" +
        $"<svg width=\"{size}\" height=\"{size}\" viewBox=\"0 0 96 96\">" +
        "<rect x=\"41\" y=\"18\" width=\"14\" height=\"20\" rx=\"6\" fill=\"#0D2620\"/><rect x=\"33\" y=\"41\" width=\"30\" height=\"5\" rx=\"2.5\" fill=\"#0D2620\"/>" +
        "<g stroke=\"#0D2620\" stroke-width=\"3\" stroke-linecap=\"round\"><line x1=\"38\" y1=\"48\" x2=\"38\" y2=\"60\"/><line x1=\"48\" y1=\"48\" x2=\"48\" y2=\"60\"/><line x1=\"58\" y1=\"48\" x2=\"58\" y2=\"60\"/></g>" +
        "<circle cx=\"38\" cy=\"72\" r=\"4\" fill=\"#0D2620\" fill-opacity=\".45\"/><circle cx=\"48\" cy=\"72\" r=\"5\" fill=\"#0D2620\"/><circle cx=\"58\" cy=\"72\" r=\"4\" fill=\"#0D2620\" fill-opacity=\".45\"/></svg></div>");

    /// <summary>An SVG text element (written in C# because Razor reads a plain text tag inside code as its own keyword).</summary>
    public static IHtmlContent SvgText(double x, double y, int size, int weight, string fill, string content) => new HtmlString(
        $"<text x=\"{Px(x)}\" y=\"{Px(y)}\" font-family=\"Inter\" font-size=\"{size}\" font-weight=\"{weight}\" fill=\"{fill}\">{WebUtility.HtmlEncode(content)}</text>");

    /// <summary>Inline number for a style attribute (always a dot, never a comma).</summary>
    public static string Px(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
