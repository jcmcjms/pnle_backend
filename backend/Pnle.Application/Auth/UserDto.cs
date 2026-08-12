namespace Pnle.Application.Auth;

public sealed record UserDto(
    Guid Id,
    string Email,
    string? Name,
    string? PictureUrl);
