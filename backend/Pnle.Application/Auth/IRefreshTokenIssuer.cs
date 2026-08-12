namespace Pnle.Application.Auth;

public interface IRefreshTokenIssuer
{
    IssuedRefreshToken Issue(
        Guid userId,
        DateTimeOffset now);
}
