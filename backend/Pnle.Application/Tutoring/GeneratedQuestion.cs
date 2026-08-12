namespace Pnle.Application.Tutoring;

public sealed record GeneratedQuestion(
    int Id,
    string Topic,
    string Difficulty,
    string Stem,
    IReadOnlyList<QuestionOption> Options);
