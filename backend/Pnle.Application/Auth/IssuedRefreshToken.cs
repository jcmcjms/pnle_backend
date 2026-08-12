using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public sealed record IssuedRefreshToken(
    string Token,
    RefreshToken Entity);
