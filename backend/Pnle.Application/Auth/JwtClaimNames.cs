namespace Pnle.Application.Auth;

/// <summary>
/// JWT claim names shared between the access token issuer and the API layer.
/// The bearer handler runs with MapInboundClaims disabled, so the claims are
/// emitted and read back using exactly these names.
/// </summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";

    public const string Email = "email";

    public const string Name = "name";

    public const string Picture = "picture";
}
