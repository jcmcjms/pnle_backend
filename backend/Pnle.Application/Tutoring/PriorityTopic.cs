namespace Pnle.Application.Tutoring;

public sealed record PriorityTopic(
    string Topic,
    string Priority,
    string Reason,
    IReadOnlyList<string> FocusAreas,
    int RecommendedMinutesPerDay);
