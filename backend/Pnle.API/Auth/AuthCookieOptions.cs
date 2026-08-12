namespace Pnle.Api.Auth;

public sealed class AuthCookieOptions
{
    public bool CookieSecure { get; init; } = true;

    public string CookieSameSite { get; init; } = "Lax";
}
