using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pnle.Application.Auth;
using Pnle.Application.Common;
using Pnle.Application.Tutoring;
using TutoringEndpoints = Pnle.Api.Tutoring.TutoringEndpoints;
using ApiCreateStudyPlanRequest = Pnle.Api.Tutoring.CreateStudyPlanRequest;
using ApiGenerateQuestionsRequest = Pnle.Api.Tutoring.GenerateQuestionsRequest;
using ApiAnswerQuestionRequest = Pnle.Api.Tutoring.AnswerQuestionRequest;
using Xunit;

namespace Pnle.Tests;

public sealed class TutoringEndpointsTests
{
    private static readonly ClaimsPrincipal AuthenticatedUser = new(
        new ClaimsIdentity(
        [
            new Claim(JwtClaimNames.Subject, "user-1")
        ],
        authenticationType: "test"));

    private static readonly ClaimsPrincipal AnonymousUser = new(
        new ClaimsIdentity(authenticationType: "test"));

    [Fact]
    public async Task CreateStudyPlan_ReturnsPlanFromAiClient()
    {
        var client = new FakeAiTutorClient
        {
            CreateStudyPlanHandler = (request, _) =>
            {
                Assert.Equal("user-1", request.UserId);
                Assert.Equal(new ScoreInput("Fluids", 3, 5), request.Scores[0]);

                return Task.FromResult(Result.Success(new StudyPlan(
                    "Summary",
                    [new PriorityTopic("Fluids", "high", "Low scores", ["Calculations"], 30)],
                    ["Review weekly"],
                    ["Pace yourself"])));
            }
        };

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest(
                [new ScoreInput("Fluids", 3, 5)],
                ExamDate: "2026-11-01",
                Notes: "Focus on calculations"),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        var ok = Assert.IsType<Ok<StudyPlan>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal("Summary", ok.Value.Summary);
        Assert.Equal("Fluids", ok.Value.PriorityTopics[0].Topic);
    }

    [Fact]
    public async Task CreateStudyPlan_WhenAiClientFails_ReturnsGeneric502()
    {
        var client = new FakeAiTutorClient
        {
            CreateStudyPlanHandler = (_, _) => Task.FromResult(
                Result.Failure<StudyPlan>(TutoringErrors.AiUpstreamUnavailable))
        };

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest([new ScoreInput("Fluids", 3, 5)], null, null),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal("AI service unavailable", problem.ProblemDetails.Title);
        Assert.DoesNotContain("upstream", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateStudyPlan_WithoutSubjectClaim_ReturnsUnauthorized()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest([new ScoreInput("Fluids", 3, 5)], null, null),
            AnonymousUser,
            client,
            CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task CreateStudyPlan_WithNoScores_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest([], null, null),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("Invalid request", problem.ProblemDetails.Title);
    }

    [Fact]
    public async Task CreateStudyPlan_WithInvalidScore_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest(
                [new ScoreInput("Fluids", Correct: 6, Total: 5)],
                null,
                null),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task CreateStudyPlan_WithInvalidExamDate_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest(
                [new ScoreInput("Fluids", 3, 5)],
                ExamDate: "not-a-date",
                Notes: null),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task CreateStudyPlan_WithOversizedNotes_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.CreateStudyPlan(
            new ApiCreateStudyPlanRequest(
                [new ScoreInput("Fluids", 3, 5)],
                ExamDate: null,
                Notes: new string('x', 2001)),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GenerateQuestions_UsesDefaultsForDifficultyAndCount()
    {
        var client = new FakeAiTutorClient
        {
            GenerateQuestionsHandler = (request, _) =>
            {
                Assert.Equal("medium", request.Difficulty);
                Assert.Equal(5, request.Count);

                return Task.FromResult(
                    Result.Success<IReadOnlyList<GeneratedQuestion>>([]));
            }
        };

        var result = await TutoringEndpoints.GenerateQuestions(
            new ApiGenerateQuestionsRequest(Topic: "Fluids", Difficulty: null, Count: null),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.IsType<Ok<IReadOnlyList<GeneratedQuestion>>>(result);
    }

    [Fact]
    public async Task GenerateQuestions_WithInvalidDifficulty_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.GenerateQuestions(
            new ApiGenerateQuestionsRequest("Fluids", "expert", 5),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GenerateQuestions_WithOutOfRangeCount_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.GenerateQuestions(
            new ApiGenerateQuestionsRequest("Fluids", null, Count: 0),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task AnswerQuestion_NormalizesOptionCase()
    {
        var client = new FakeAiTutorClient
        {
            AnswerQuestionHandler = (request, questionId, _) =>
            {
                Assert.Equal(42, questionId);
                Assert.Equal("A", request.SelectedOptionId);

                return Task.FromResult(Result.Success(new AnswerEvaluation(
                    42,
                    IsCorrect: true,
                    CorrectOptionId: "A",
                    Rationale: "Because.",
                    CommonMistake: "Confusion.")));
            }
        };

        var result = await TutoringEndpoints.AnswerQuestion(
            questionId: 42,
            new ApiAnswerQuestionRequest(SelectedOptionId: "a"),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.IsType<Ok<AnswerEvaluation>>(result);
    }

    [Fact]
    public async Task AnswerQuestion_WithInvalidOption_ReturnsBadRequest()
    {
        var client = new FakeAiTutorClient();

        var result = await TutoringEndpoints.AnswerQuestion(
            questionId: 42,
            new ApiAnswerQuestionRequest(SelectedOptionId: "e"),
            AuthenticatedUser,
            client,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetWeaknesses_ReturnsReport()
    {
        var client = new FakeAiTutorClient
        {
            GetWeaknessesHandler = (userId, _) =>
            {
                Assert.Equal("user-1", userId);

                return Task.FromResult(Result.Success(new WeaknessReport(
                    "user-1",
                    [new TopicScoreSummary("Fluids", 3, 5, 60.0)],
                    ["Fluids"],
                    DateTimeOffset.UtcNow)));
            }
        };

        var result = await TutoringEndpoints.GetWeaknesses(
            AuthenticatedUser,
            client,
            CancellationToken.None);

        var ok = Assert.IsType<Ok<WeaknessReport>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal("Fluids", ok.Value.WeakTopics.Single());
    }

    [Fact]
    public async Task GetReadiness_WhenAiClientRejects_Returns502WithRejectedTitle()
    {
        var client = new FakeAiTutorClient
        {
            GetReadinessHandler = (_, _) => Task.FromResult(
                Result.Failure<ReadinessReport>(TutoringErrors.AiUpstreamRejected))
        };

        var result = await TutoringEndpoints.GetReadiness(
            AuthenticatedUser,
            client,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal("AI service rejected the request", problem.ProblemDetails.Title);
    }

    private sealed class FakeAiTutorClient : IAiTutorClient
    {
        public Func<StudyPlanRequest, CancellationToken, Task<Result<StudyPlan>>>?
            CreateStudyPlanHandler { get; set; }

        public Func<GenerateQuestionsRequest, CancellationToken,
            Task<Result<IReadOnlyList<GeneratedQuestion>>>>?
            GenerateQuestionsHandler { get; set; }

        public Func<AnswerQuestionRequest, int, CancellationToken, Task<Result<AnswerEvaluation>>>?
            AnswerQuestionHandler { get; set; }

        public Func<string, CancellationToken, Task<Result<WeaknessReport>>>?
            GetWeaknessesHandler { get; set; }

        public Func<string, CancellationToken, Task<Result<ReadinessReport>>>?
            GetReadinessHandler { get; set; }

        public Task<Result<StudyPlan>> CreateStudyPlanAsync(
            StudyPlanRequest request,
            CancellationToken cancellationToken) =>
            CreateStudyPlanHandler?.Invoke(request, cancellationToken) ??
            throw new InvalidOperationException("CreateStudyPlanHandler not configured.");

        public Task<Result<IReadOnlyList<GeneratedQuestion>>> GenerateQuestionsAsync(
            GenerateQuestionsRequest request,
            CancellationToken cancellationToken) =>
            GenerateQuestionsHandler?.Invoke(request, cancellationToken) ??
            throw new InvalidOperationException("GenerateQuestionsHandler not configured.");

        public Task<Result<AnswerEvaluation>> AnswerQuestionAsync(
            AnswerQuestionRequest request,
            int questionId,
            CancellationToken cancellationToken) =>
            AnswerQuestionHandler?.Invoke(request, questionId, cancellationToken) ??
            throw new InvalidOperationException("AnswerQuestionHandler not configured.");

        public Task<Result<WeaknessReport>> GetWeaknessesAsync(
            string userId,
            CancellationToken cancellationToken) =>
            GetWeaknessesHandler?.Invoke(userId, cancellationToken) ??
            throw new InvalidOperationException("GetWeaknessesHandler not configured.");

        public Task<Result<ReadinessReport>> GetReadinessAsync(
            string userId,
            CancellationToken cancellationToken) =>
            GetReadinessHandler?.Invoke(userId, cancellationToken) ??
            throw new InvalidOperationException("GetReadinessHandler not configured.");
    }
}
