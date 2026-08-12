namespace Pnle.Application.Tutoring;

public sealed record WeaknessReport(
    string UserId,
    IReadOnlyList<TopicScoreSummary> Topics,
    IReadOnlyList<string> WeakTopics,
    DateTimeOffset GeneratedAt);
