using Pnle.Application.Common;

namespace Pnle.Application.Auth;

public sealed record RefreshSessionCommand(string RefreshToken);

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

        var tokenHash = refreshTokenHasher.Hash(command.RefreshToken);

        var storedToken = await refreshTokenRepository.FindByHashAsync(
            tokenHash,
            cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (storedToken is null || !storedToken.IsActive(now))
        {
            return Result.Failure<AuthSession>(AuthErrors.InvalidRefreshToken);
        }

        var user = await userRepository.FindByIdAsync(
            storedToken.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthSession>(AuthErrors.UserNotFound);
        }

        storedToken.Revoke(now);

        var newRefreshToken = refreshTokenIssuer.Issue(user.Id, now);

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

        var tokenHash = refreshTokenHasher.Hash(command.RefreshToken);

        var storedToken = await refreshTokenRepository.FindByHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null)
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();

        if (storedToken.IsActive(now))
        {
            storedToken.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}