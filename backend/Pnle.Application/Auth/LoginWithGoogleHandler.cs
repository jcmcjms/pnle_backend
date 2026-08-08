using Microsoft.Extensions.Logging;
using Pnle.Application.Common;
using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public sealed class LoginWithGoogleHandler(
    IGoogleTokenValidator googleTokenValidator,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenIssuer refreshTokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<LoginWithGoogleHandler> logger)
{
    public async Task<Result<AuthSession>> HandleAsync(
        LoginWithGoogleCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdToken))
        {
            return Result.Failure<AuthSession>(AuthErrors.InvalidGoogleToken);
        }

        var googleResult = await googleTokenValidator.ValidateAsync(
            command.IdToken,
            cancellationToken);

        if (googleResult.IsFailure)
        {
            return Result.Failure<AuthSession>(googleResult.Errors);
        }

        var profile = googleResult.Value;
        var now = timeProvider.GetUtcNow();

        var user = await userRepository.FindByGoogleSubjectAsync(
            profile.Subject,
            cancellationToken);

        if (user is null)
        {
            user = User.CreateFromGoogle(profile, now);

            await userRepository.AddAsync(user, cancellationToken);

            logger.LogInformation(
                "New user created from Google login: {UserId}",
                user.Id);
        }
        else
        {
            user.UpdateFromGoogle(profile, now);
        }

        var accessToken = accessTokenIssuer.Issue(user);

        var refreshToken = refreshTokenIssuer.Issue(user.Id, now);

        await refreshTokenRepository.AddAsync(refreshToken.Entity, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var session = new AuthSession(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.Entity.ExpiresAtUtc,
            new UserDto(
                user.Id,
                user.Email,
                user.Name,
                user.PictureUrl));

        return Result.Success(session);
    }
}