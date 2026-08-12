namespace Pnle.Application.Tutoring;

public sealed record StudyPlanRequest(
    string UserId,
    IReadOnlyList<ScoreInput> Scores,
    string? ExamDate,
    string? Notes);
