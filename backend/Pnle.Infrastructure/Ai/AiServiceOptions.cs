namespace Pnle.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public required string BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    public int TimeoutSeconds { get; init; } = 60;
}
