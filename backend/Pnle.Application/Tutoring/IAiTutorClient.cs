using Pnle.Application.Common;

namespace Pnle.Application.Tutoring;

public interface IAiTutorClient
{
    Task<Result<StudyPlan>> CreateStudyPlanAsync(
        StudyPlanRequest request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<GeneratedQuestion>>> GenerateQuestionsAsync(
        GenerateQuestionsRequest request,
        CancellationToken cancellationToken);

    Task<Result<AnswerEvaluation>> AnswerQuestionAsync(
        AnswerQuestionRequest request,
        int questionId,
        CancellationToken cancellationToken);

    Task<Result<WeaknessReport>> GetWeaknessesAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<Result<ReadinessReport>> GetReadinessAsync(
        string userId,
        CancellationToken cancellationToken);
}
