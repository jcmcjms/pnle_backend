using System.Security.Cryptography;
using System.Text;
using Pnle.Application.Auth;

namespace Pnle.Infrastructure.Auth;

public sealed class Sha256RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}