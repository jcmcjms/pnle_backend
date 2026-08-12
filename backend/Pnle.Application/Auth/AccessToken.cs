namespace Pnle.Application.Auth;

public sealed record AccessToken(
    string Token,
    DateTimeOffset ExpiresAtUtc);
