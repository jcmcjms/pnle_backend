using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken);
}