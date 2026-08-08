using Microsoft.Extensions.Options;

namespace Pnle.Api.Auth;

public sealed class AuthCookieOptions
{
    public bool CookieSecure { get; init; } = true;

    public string CookieSameSite { get; init; } = "Lax";
}

public sealed class RefreshCookieWriter(IOptions<AuthCookieOptions> options)
{
    public const string RefreshCookieName = "pnle_refresh_token";

    public void Set(
        HttpContext httpContext,
        string token,
        DateTimeOffset expiresAtUtc)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(
            options.Value.CookieSameSite,
            ignoreCase: true,
            out var parsedSameSite)
                ? parsedSameSite
                : SameSiteMode.Lax;

        httpContext.Response.Cookies.Append(
            RefreshCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = options.Value.CookieSecure,
                SameSite = sameSite,
                Expires = expiresAtUtc,
                Path = "/auth"
            });
    }

    public void Delete(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                Path = "/auth"
            });
    }
}
