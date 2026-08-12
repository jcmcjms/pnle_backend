namespace Pnle.Application.Tutoring;

public sealed record ReadinessReport(
    string UserId,
    double OverallScore,
    string Status,
    IReadOnlyList<TopicScoreSummary> Topics,
    IReadOnlyList<string> WeakTopics,
    DateTimeOffset GeneratedAt);
