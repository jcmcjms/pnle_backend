using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public interface IRefreshTokenIssuer
{
    IssuedRefreshToken Issue(
        Guid userId,
        DateTimeOffset now);
}

public sealed record IssuedRefreshToken(
    string Token,
    RefreshToken Entity);

public interface IRefreshTokenHasher
{
    string Hash(string token);
}