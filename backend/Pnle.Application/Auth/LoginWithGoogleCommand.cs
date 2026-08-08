namespace Pnle.Application.Auth;

public sealed record LoginWithGoogleCommand(string IdToken);

public sealed record UserDto(
    Guid Id,
    string Email,
    string? Name,
    string? PictureUrl);

public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    UserDto User);