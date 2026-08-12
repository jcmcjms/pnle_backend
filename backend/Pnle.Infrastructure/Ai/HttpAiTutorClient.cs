using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pnle.Application.Common;
using Pnle.Application.Tutoring;

namespace Pnle.Infrastructure.Ai;

public sealed class HttpAiTutorClient : IAiTutorClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAiTutorClient> _logger;

    public HttpAiTutorClient(
        HttpClient httpClient,
        ILogger<HttpAiTutorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<Result<StudyPlan>> CreateStudyPlanAsync(
        StudyPlanRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<StudyPlan>("/v1/tutor/plan", request, cancellationToken);

    public Task<Result<IReadOnlyList<GeneratedQuestion>>> GenerateQuestionsAsync(
        GenerateQuestionsRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<IReadOnlyList<GeneratedQuestion>>(
            "/v1/questions/generate", request, cancellationToken);

    public Task<Result<AnswerEvaluation>> AnswerQuestionAsync(
        AnswerQuestionRequest request,
        int questionId,
        CancellationToken cancellationToken) =>
        PostAsync<AnswerEvaluation>(
            $"/v1/questions/{questionId}/answer", request, cancellationToken);

    public Task<Result<WeaknessReport>> GetWeaknessesAsync(
        string userId,
        CancellationToken cancellationToken) =>
        GetAsync<WeaknessReport>(
            $"/v1/users/{Uri.EscapeDataString(userId)}/weaknesses", cancellationToken);

    public Task<Result<ReadinessReport>> GetReadinessAsync(
        string userId,
        CancellationToken cancellationToken) =>
        GetAsync<ReadinessReport>(
            $"/v1/users/{Uri.EscapeDataString(userId)}/readiness", cancellationToken);

    private async Task<Result<TValue>> PostAsync<TValue>(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        var content = JsonContent.Create(body, options: JsonOptions);

        return await SendAsync<TValue>(
            () => _httpClient.PostAsync(path, content, cancellationToken),
            path,
            cancellationToken);
    }

    private Task<Result<TValue>> GetAsync<TValue>(
        string path,
        CancellationToken cancellationToken) =>
        SendAsync<TValue>(
            () => _httpClient.GetAsync(path, cancellationToken),
            path,
            cancellationToken);

    private async Task<Result<TValue>> SendAsync<TValue>(
        Func<Task<HttpResponseMessage>> send,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI service returned {StatusCode} for {Path}.",
                    (int)response.StatusCode,
                    path);

                return Result.Failure<TValue>(MapUpstreamFailure(response.StatusCode));
            }

            return await ParseSuccessAsync<TValue>(response, path, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "AI service request to {Path} failed at the transport level.",
                path);

            return Result.Failure<TValue>(TutoringErrors.AiUpstreamUnavailable);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "AI service request to {Path} timed out.",
                path);

            return Result.Failure<TValue>(TutoringErrors.AiUpstreamUnavailable);
        }
    }

    private async Task<Result<TValue>> ParseSuccessAsync<TValue>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var value = JsonSerializer.Deserialize<TValue>(body, JsonOptions);

            if (value is null)
            {
                _logger.LogWarning(
                    "AI service returned an empty payload for {Path}.",
                    path);

                return Result.Failure<TValue>(TutoringErrors.AiUpstreamError);
            }

            return Result.Success(value);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "AI service returned a malformed payload for {Path}.",
                path);

            return Result.Failure<TValue>(TutoringErrors.AiUpstreamError);
        }
    }

    private static Error MapUpstreamFailure(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout => TutoringErrors.AiUpstreamUnavailable,

            HttpStatusCode.BadRequest or
            HttpStatusCode.Forbidden or
            HttpStatusCode.NotFound or
            HttpStatusCode.TooManyRequests => TutoringErrors.AiUpstreamRejected,

            _ => TutoringErrors.AiUpstreamError
        };
    }
}
