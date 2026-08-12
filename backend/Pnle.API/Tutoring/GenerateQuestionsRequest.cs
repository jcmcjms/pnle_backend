namespace Pnle.Api.Tutoring;

public sealed record GenerateQuestionsRequest(
    string? Topic,
    string? Difficulty,
    int? Count);
