namespace Pnle.Infrastructure.Auth;

public sealed class RefreshTokenOptions
{
    public int RefreshTokenDays { get; init; } = 30;
}
