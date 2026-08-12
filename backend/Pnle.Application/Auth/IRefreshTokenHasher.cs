namespace Pnle.Application.Auth;

public interface IRefreshTokenHasher
{
    string Hash(string token);
}
