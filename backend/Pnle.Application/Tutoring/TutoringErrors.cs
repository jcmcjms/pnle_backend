using Pnle.Application.Common;

namespace Pnle.Application.Tutoring;

public static class TutoringErrors
{
    public static readonly Error AiUpstreamUnavailable = new(
        "AI_UPSTREAM_UNAVAILABLE",
        "The AI study service is temporarily unavailable.",
        ErrorType.Unexpected);

    public static readonly Error AiUpstreamRejected = new(
        "AI_UPSTREAM_REJECTED",
        "The AI study service rejected the request.",
        ErrorType.Unexpected);

    public static readonly Error AiUpstreamError = new(
        "AI_UPSTREAM_ERROR",
        "The AI study service returned an unexpected response.",
        ErrorType.Unexpected);
}
