namespace Pnle.Domain.Auth;

public sealed record GoogleUserProfile(
    string Subject,
    string Email,
    string? Name,
    string? PictureUrl);