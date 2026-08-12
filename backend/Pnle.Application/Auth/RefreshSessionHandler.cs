using Pnle.Application.Common;
using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public sealed class RefreshSessionHandler(
    IRefreshTokenHasher refreshTokenHasher,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenIssuer refreshTokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<AuthSession>> RefreshAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Failure<AuthSession>(AuthErrors.InvalidRefreshToken);
        }

        var activeToken = await FindActiveRefreshTokenAsync(
            command.RefreshToken,
            cancellationToken);

        if (activeToken is null)
        {
            return Result.Failure<AuthSession>(AuthErrors.InvalidRefreshToken);
        }

        var user = await userRepository.FindByIdAsync(
            activeToken.Token.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthSession>(AuthErrors.UserNotFound);
        }

        activeToken.Token.Revoke(activeToken.Now);

        var newRefreshToken = refreshTokenIssuer.Issue(user.Id, activeToken.Now);

        await refreshTokenRepository.AddAsync(newRefreshToken.Entity, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenIssuer.Issue(user);

        var session = new AuthSession(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            newRefreshToken.Token,
            newRefreshToken.Entity.ExpiresAtUtc,
            new UserDto(
                user.Id,
                user.Email,
                user.Name,
                user.PictureUrl));

        return Result.Success(session);
    }

    public async Task<Result> LogoutAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        var activeToken = await FindActiveRefreshTokenAsync(
            command.RefreshToken,
            cancellationToken);

        if (activeToken is null)
        {
            return Result.Success();
        }

        activeToken.Token.Revoke(activeToken.Now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<ActiveRefreshToken?> FindActiveRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenHasher.Hash(rawRefreshToken);

        var storedToken = await refreshTokenRepository.FindByHashAsync(
            tokenHash,
            cancellationToken);

        var now = timeProvider.GetUtcNow();

        return storedToken is { } token && token.IsActive(now)
            ? new ActiveRefreshToken(token, now)
            : null;
    }

    private sealed record ActiveRefreshToken(RefreshToken Token, DateTimeOffset Now);
}
