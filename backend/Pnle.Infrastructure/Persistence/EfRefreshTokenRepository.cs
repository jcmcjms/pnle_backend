using Microsoft.EntityFrameworkCore;
using Pnle.Application.Auth;
using Pnle.Domain.Auth;

namespace Pnle.Infrastructure.Persistence;

public sealed class EfRefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        return db.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    }

    public async Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        await db.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }
}