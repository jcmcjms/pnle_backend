namespace Pnle.Application.Tutoring;

public sealed record StudyPlan(
    string Summary,
    IReadOnlyList<PriorityTopic> PriorityTopics,
    IReadOnlyList<string> WeeklyActions,
    IReadOnlyList<string> TestTakingStrategy);
