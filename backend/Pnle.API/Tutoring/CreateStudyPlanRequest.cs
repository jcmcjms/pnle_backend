using Pnle.Application.Tutoring;

namespace Pnle.Api.Tutoring;

public sealed record CreateStudyPlanRequest(
    IReadOnlyList<ScoreInput>? Scores,
    string? ExamDate,
    string? Notes);
