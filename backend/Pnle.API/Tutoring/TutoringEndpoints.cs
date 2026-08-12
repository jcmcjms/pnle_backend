using System.Globalization;
using System.Security.Claims;
using Pnle.Application.Auth;
using Pnle.Application.Common;
using Pnle.Application.Tutoring;
using AiGenerateQuestionsRequest = Pnle.Application.Tutoring.GenerateQuestionsRequest;
using AiAnswerQuestionRequest = Pnle.Application.Tutoring.AnswerQuestionRequest;

namespace Pnle.Api.Tutoring;

public static class TutoringEndpoints
{
    private const string DifficultyEasy = "easy";
    private const string DifficultyMedium = "medium";
    private const string DifficultyHard = "hard";

    private static readonly string[] ValidDifficulties =
        [DifficultyEasy, DifficultyMedium, DifficultyHard];

    private static readonly string[] ValidOptionIds = ["A", "B", "C", "D"];

    public static IEndpointRouteBuilder MapTutoringEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var tutoring = endpoints
            .MapGroup("/tutoring")
            .WithTags("Tutoring")
            .RequireAuthorization()
            .RequireRateLimiting("ai");

        tutoring.MapPost("/study-plan", CreateStudyPlan);
        tutoring.MapPost("/questions/generate", GenerateQuestions);
        tutoring.MapPost("/questions/{questionId:int}/answer", AnswerQuestion);
        tutoring.MapGet("/weaknesses", GetWeaknesses);
        tutoring.MapGet("/readiness", GetReadiness);

        return endpoints;
    }

    public static async Task<IResult> CreateStudyPlan(
        CreateStudyPlanRequest request,
        ClaimsPrincipal user,
        IAiTutorClient aiTutorClient,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserId(user);

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var scores = request.Scores;

        if (scores is not { Count: > 0 })
        {
            return InvalidRequest("At least one topic score is required.");
        }

        foreach (var score in scores)
        {
            if (string.IsNullOrWhiteSpace(score.Topic) || score.Topic.Length > 120)
            {
                return InvalidRequest("Each score topic must be between 1 and 120 characters.");
            }

            if (score.Correct < 0 || score.Total <= 0 || score.Correct > score.Total)
            {
                return InvalidRequest("Each score must satisfy 0 <= correct <= total and total > 0.");
            }
        }

        if (request.ExamDate is not null &&
            !DateOnly.TryParse(
                request.ExamDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return InvalidRequest("examDate must be a valid date in YYYY-MM-DD format.");
        }

        if (request.Notes is { Length: > 2000 })
        {
            return InvalidRequest("notes must be 2000 characters or fewer.");
        }

        var aiRequest = new StudyPlanRequest(userId, scores, request.ExamDate, request.Notes);
        var result = await aiTutorClient.CreateStudyPlanAsync(aiRequest, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : MapAiFailure(result.Errors);
    }

    public static async Task<IResult> GenerateQuestions(
        GenerateQuestionsRequest request,
        ClaimsPrincipal user,
        IAiTutorClient aiTutorClient,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserId(user);

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Topic) || request.Topic.Length > 120)
        {
            return InvalidRequest("topic must be between 1 and 120 characters.");
        }

        var difficulty = NormalizeDifficulty(request.Difficulty);

        if (difficulty is null)
        {
            return InvalidRequest("difficulty must be one of: easy, medium, hard.");
        }

        var count = request.Count ?? 5;

        if (count is < 1 or > 10)
        {
            return InvalidRequest("count must be between 1 and 10.");
        }

        var aiRequest = new AiGenerateQuestionsRequest(userId, request.Topic, difficulty, count);
        var result = await aiTutorClient.GenerateQuestionsAsync(aiRequest, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : MapAiFailure(result.Errors);
    }

    public static async Task<IResult> AnswerQuestion(
        int questionId,
        AnswerQuestionRequest request,
        ClaimsPrincipal user,
        IAiTutorClient aiTutorClient,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserId(user);

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var selectedOptionId = request.SelectedOptionId?.Trim().ToUpperInvariant();

        if (selectedOptionId is null || !ValidOptionIds.Contains(selectedOptionId))
        {
            return InvalidRequest("selectedOptionId must be one of: A, B, C, D.");
        }

        var aiRequest = new AiAnswerQuestionRequest(userId, selectedOptionId);
        var result = await aiTutorClient.AnswerQuestionAsync(
            aiRequest,
            questionId,
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : MapAiFailure(result.Errors);
    }

    public static async Task<IResult> GetWeaknesses(
        ClaimsPrincipal user,
        IAiTutorClient aiTutorClient,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserId(user);

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await aiTutorClient.GetWeaknessesAsync(userId, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : MapAiFailure(result.Errors);
    }

    public static async Task<IResult> GetReadiness(
        ClaimsPrincipal user,
        IAiTutorClient aiTutorClient,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserId(user);

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await aiTutorClient.GetReadinessAsync(userId, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : MapAiFailure(result.Errors);
    }

    private static string? NormalizeDifficulty(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return DifficultyMedium;
        }

        var normalized = difficulty.Trim().ToLowerInvariant();

        return ValidDifficulties.Contains(normalized) ? normalized : null;
    }

    private static string? ReadUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtClaimNames.Subject)?.Value;

        return string.IsNullOrWhiteSpace(sub) ? null : sub;
    }

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid request",
            detail: detail);

    private static IResult MapAiFailure(IReadOnlyList<Error> errors)
    {
        var error = errors.FirstOrDefault();

        if (error?.Code == TutoringErrors.AiUpstreamUnavailable.Code)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI service unavailable",
                detail: "The AI study service is temporarily unavailable. Please try again later.");
        }

        if (error?.Code == TutoringErrors.AiUpstreamRejected.Code)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI service rejected the request",
                detail: "The AI study service could not process the request.");
        }

        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "AI service error",
            detail: "An unexpected error occurred while contacting the AI study service.");
    }
}
