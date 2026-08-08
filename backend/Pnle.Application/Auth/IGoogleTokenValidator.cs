using Pnle.Application.Common;
using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public interface IGoogleTokenValidator
{
    Task<Result<GoogleUserProfile>> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken);
}