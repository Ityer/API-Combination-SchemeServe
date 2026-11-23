using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace APICombination.Services;

public sealed class StackExchangeClient(HttpClient httpClient, IOptions<ApiCredentials> credentials) : IStackExchangeClient
{
    private readonly string? apiKey = string.IsNullOrWhiteSpace(credentials.Value.StackExchangeKey)
        ? null
        : credentials.Value.StackExchangeKey.Trim();

    public async Task<IReadOnlyList<StackOverflowQuestion>> SearchQuestionsAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var keyParameter = apiKey is null ? string.Empty : $"&key={Uri.EscapeDataString(apiKey)}";
        var uri = $"search/advanced?site=stackoverflow&order=desc&sort=relevance&pagesize=20&q={Uri.EscapeDataString(query)}{keyParameter}";
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt == 2)
                    {
                        throw new ApiRequestException("Stack Overflow rate limit was reached after one retry.", (int)response.StatusCode);
                    }

                    await DelayForRetryAsync(response.Headers.RetryAfter?.Delta, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiRequestException(
                        $"Stack Overflow returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                        (int)response.StatusCode);
                }

                var result = await response.Content.ReadFromJsonAsync<StackExchangeResponse>(cancellationToken: cancellationToken)
                    ?? throw new ApiRequestException("Stack Overflow returned an empty response.");

                if (result.Backoff is > 0)
                {
                    if (attempt == 2)
                    {
                        throw new ApiRequestException($"Stack Overflow requested another backoff after one retry ({result.Backoff} seconds).");
                    }

                    await DelayForRetryAsync(TimeSpan.FromSeconds(result.Backoff.Value), cancellationToken);
                    continue;
                }

                if (result.ErrorId is not null)
                {
                    throw new ApiRequestException($"Stack Overflow API error {result.ErrorId}: {result.ErrorMessage ?? "unknown error"}.");
                }

                return result.Items.Select(item => new StackOverflowQuestion(
                    item.QuestionId,
                    item.Title,
                    item.Link,
                    item.Score,
                    item.AnswerCount,
                    item.IsAnswered,
                    item.Tags)).ToArray();
            }

            throw new ApiRequestException("Stack Overflow search did not complete.");
        }
        catch (HttpRequestException exception)
        {
            throw new ApiRequestException($"Stack Overflow could not be reached: {exception.Message}", null);
        }
    }

    private static Task DelayForRetryAsync(TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var delay = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
            ? retryAfter.Value
            : TimeSpan.FromSeconds(1);
        return Task.Delay(delay, cancellationToken);
    }

    private sealed class StackExchangeResponse
    {
        [JsonPropertyName("items")] public List<QuestionResponse> Items { get; set; } = [];
        [JsonPropertyName("backoff")] public int? Backoff { get; set; }
        [JsonPropertyName("error_id")] public int? ErrorId { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }

    private sealed class QuestionResponse
    {
        [JsonPropertyName("question_id")] public int QuestionId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("link")] public string Link { get; set; } = string.Empty;
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("answer_count")] public int AnswerCount { get; set; }
        [JsonPropertyName("is_answered")] public bool IsAnswered { get; set; }
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    }
}
