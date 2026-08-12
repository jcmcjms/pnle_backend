using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Pnle.Application.Auth;
using Pnle.Domain.Auth;

namespace Pnle.Infrastructure.Auth;

public sealed class RefreshTokenIssuer(
    IOptions<RefreshTokenOptions> options,
    IRefreshTokenHasher refreshTokenHasher)
    : IRefreshTokenIssuer
{
    public IssuedRefreshToken Issue(
        Guid userId,
        DateTimeOffset now)
    {
        var tokenValue = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        var tokenHash = refreshTokenHasher.Hash(tokenValue);

        var expiresAtUtc = now.AddDays(options.Value.RefreshTokenDays);

        var entity = RefreshToken.Create(
            userId,
            tokenHash,
            now,
            expiresAtUtc);

        return new IssuedRefreshToken(tokenValue, entity);
    }
}
