namespace Pnle.Application.Tutoring;

public sealed record GenerateQuestionsRequest(
    string UserId,
    string Topic,
    string Difficulty,
    int Count);
