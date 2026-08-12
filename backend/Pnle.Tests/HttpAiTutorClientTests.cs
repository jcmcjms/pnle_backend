using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pnle.Application.Common;
using Pnle.Application.Tutoring;
using Pnle.Infrastructure.Ai;
using Xunit;

namespace Pnle.Tests;

public sealed class HttpAiTutorClientTests
{
    private const string ApiKey = "test-api-key";

    [Fact]
    public async Task CreateStudyPlanAsync_SendsApiKeyHeaderAndSnakeCaseBody()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var client = CreateClient(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return SuccessResponse("""
                {
                    "summary": "Study plan",
                    "priority_topics": [
                        {
                            "topic": "Fluids",
                            "priority": "high",
                            "reason": "Low scores",
                            "focus_areas": ["Calculations"],
                            "recommended_minutes_per_day": 30
                        }
                    ],
                    "weekly_actions": ["Review weekly"],
                    "test_taking_strategy": ["Pace yourself"]
                }
                """);
        });

        var request = new StudyPlanRequest(
            "user-1",
            [new ScoreInput("Fluids", 3, 5)],
            ExamDate: "2026-11-01",
            Notes: "Focus on calculations");

        var result = await client.CreateStudyPlanAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("/v1/tutor/plan", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal(ApiKey, capturedRequest.Headers.GetValues("x-api-key").Single());
        Assert.Contains("\"user_id\":\"user-1\"", capturedBody);
        Assert.Contains("\"exam_date\":\"2026-11-01\"", capturedBody);
        Assert.Equal("Study plan", result.Value.Summary);
        Assert.Equal(30, result.Value.PriorityTopics[0].RecommendedMinutesPerDay);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_ParsesQuestionList()
    {
        var client = CreateClient(async _ => SuccessResponse("""
            [
                {
                    "id": 42,
                    "topic": "Fluids",
                    "difficulty": "medium",
                    "stem": "Which statement is correct?",
                    "options": [
                        { "id": "A", "text": "First" },
                        { "id": "B", "text": "Second" }
                    ]
                }
            ]
            """));

        var result = await client.GenerateQuestionsAsync(
            new GenerateQuestionsRequest("user-1", "Fluids", "medium", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(result.Value);
        Assert.Equal(42, question.Id);
        Assert.Equal("Fluids", question.Topic);
        Assert.Equal(2, question.Options.Count);
        Assert.Equal("A", question.Options[0].Id);
    }

    [Fact]
    public async Task AnswerQuestionAsync_UsesRouteQuestionId()
    {
        HttpRequestMessage? capturedRequest = null;

        var client = CreateClient(async request =>
        {
            capturedRequest = request;
            return SuccessResponse("""
                {
                    "question_id": 42,
                    "is_correct": true,
                    "correct_option_id": "A",
                    "rationale": "Archimedes' principle explains it.",
                    "common_mistake": "Confusing buoyancy with density."
                }
                """);
        });

        var result = await client.AnswerQuestionAsync(
            new AnswerQuestionRequest("user-1", "A"),
            questionId: 42,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/v1/questions/42/answer", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.True(result.Value.IsCorrect);
        Assert.Equal("A", result.Value.CorrectOptionId);
    }

    [Fact]
    public async Task GetWeaknessesAsync_GetsUserPathAndParsesReport()
    {
        HttpRequestMessage? capturedRequest = null;

        var client = CreateClient(async request =>
        {
            capturedRequest = request;
            return SuccessResponse("""
                {
                    "user_id": "user-1",
                    "topics": [
                        { "topic": "Fluids", "correct": 3, "total": 5, "percent": 60.0 }
                    ],
                    "weak_topics": ["Fluids"],
                    "generated_at": "2026-08-12T10:00:00Z"
                }
                """);
        });

        var result = await client.GetWeaknessesAsync("user-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/v1/users/user-1/weaknesses", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Fluids", result.Value.WeakTopics.Single());
        Assert.Equal(60.0, result.Value.Topics[0].Percent);
        Assert.Equal(new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero), result.Value.GeneratedAt);
    }

    [Theory]
    [MemberData(nameof(UpstreamStatusCases))]
    public async Task CreateStudyPlanAsync_MapsUpstreamStatus(
        HttpStatusCode statusCode,
        Error expectedError)
    {
        var client = CreateClient(async _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("""{"detail":"upstream detail"}""")
        });

        var result = await client.CreateStudyPlanAsync(
            new StudyPlanRequest("user-1", [new ScoreInput("Fluids", 3, 5)], null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError.Code, result.Errors[0].Code);
    }

    public static TheoryData<HttpStatusCode, Error> UpstreamStatusCases() => new()
    {
        { HttpStatusCode.Unauthorized, TutoringErrors.AiUpstreamUnavailable },
        { HttpStatusCode.BadGateway, TutoringErrors.AiUpstreamUnavailable },
        { HttpStatusCode.ServiceUnavailable, TutoringErrors.AiUpstreamUnavailable },
        { HttpStatusCode.GatewayTimeout, TutoringErrors.AiUpstreamUnavailable },
        { HttpStatusCode.BadRequest, TutoringErrors.AiUpstreamRejected },
        { HttpStatusCode.Forbidden, TutoringErrors.AiUpstreamRejected },
        { HttpStatusCode.NotFound, TutoringErrors.AiUpstreamRejected },
        { HttpStatusCode.TooManyRequests, TutoringErrors.AiUpstreamRejected },
        { HttpStatusCode.InternalServerError, TutoringErrors.AiUpstreamError }
    };

    [Fact]
    public async Task CreateStudyPlanAsync_MalformedSuccessPayload_ReturnsUpstreamError()
    {
        var client = CreateClient(async _ => SuccessResponse("{ not json"));

        var result = await client.CreateStudyPlanAsync(
            new StudyPlanRequest("user-1", [new ScoreInput("Fluids", 3, 5)], null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TutoringErrors.AiUpstreamError.Code, result.Errors[0].Code);
    }

    [Fact]
    public async Task CreateStudyPlanAsync_TransportFailure_ReturnsUpstreamUnavailable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        var result = await client.CreateStudyPlanAsync(
            new StudyPlanRequest("user-1", [new ScoreInput("Fluids", 3, 5)], null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TutoringErrors.AiUpstreamUnavailable.Code, result.Errors[0].Code);
    }

    [Fact]
    public async Task CreateStudyPlanAsync_Timeout_ReturnsUpstreamUnavailable()
    {
        var client = CreateClient(_ => throw new TaskCanceledException("timed out"));

        var result = await client.CreateStudyPlanAsync(
            new StudyPlanRequest("user-1", [new ScoreInput("Fluids", 3, 5)], null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TutoringErrors.AiUpstreamUnavailable.Code, result.Errors[0].Code);
    }

    [Fact]
    public async Task CreateStudyPlanAsync_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var client = CreateClient(
            _ => throw new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CreateStudyPlanAsync(
                new StudyPlanRequest("user-1", [new ScoreInput("Fluids", 3, 5)], null, null),
                cancellation.Token));
    }

    private static HttpAiTutorClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var apiKeyHandler = new AiApiKeyDelegatingHandler(Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://localhost:8000",
            ApiKey = ApiKey
        }))
        {
            InnerHandler = new FakeHttpMessageHandler((request, _) => handler(request))
        };

        var httpClient = new HttpClient(apiKeyHandler)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        return new HttpAiTutorClient(httpClient, NullLogger<HttpAiTutorClient>.Instance);
    }

    private static HttpResponseMessage SuccessResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
