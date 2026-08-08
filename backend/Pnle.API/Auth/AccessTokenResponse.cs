using Pnle.Application.Auth;

namespace Pnle.Api.Auth;

public sealed record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    UserDto User);
