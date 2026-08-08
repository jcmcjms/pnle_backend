using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pnle.Application.Auth;
using Pnle.Application.Common;
using Pnle.Domain.Auth;

namespace Pnle.Infrastructure.Auth;

public sealed class GoogleOptions
{
    public required string[] ClientIds { get; init; }
}

public sealed class GoogleTokenValidator(
    IOptions<GoogleOptions> options,
    ILogger<GoogleTokenValidator> logger)
    : IGoogleTokenValidator
{
    public async Task<Result<GoogleUserProfile>> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Result.Failure<GoogleUserProfile>(AuthErrors.InvalidGoogleToken);
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = options.Value.ClientIds
                });

            if (payload.EmailVerified != true)
            {
                return Result.Failure<GoogleUserProfile>(AuthErrors.EmailNotVerified);
            }

            var email = payload.Email?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(email))
            {
                return Result.Failure<GoogleUserProfile>(AuthErrors.InvalidGoogleToken);
            }

            return Result.Success(new GoogleUserProfile(
                payload.Subject,
                email,
                payload.Name,
                payload.Picture));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google ID token validation failed.");

            return Result.Failure<GoogleUserProfile>(AuthErrors.InvalidGoogleToken);
        }
    }
}