namespace Pnle.Application.Tutoring;

public sealed record AnswerEvaluation(
    int QuestionId,
    bool IsCorrect,
    string CorrectOptionId,
    string Rationale,
    string CommonMistake);
