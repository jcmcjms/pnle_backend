using Microsoft.Extensions.Options;

namespace Pnle.Infrastructure.Ai;

public sealed class AiApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly AiServiceOptions _options;

    public AiApiKeyDelegatingHandler(IOptions<AiServiceOptions> options)
    {
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
