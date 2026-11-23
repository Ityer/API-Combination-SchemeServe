using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace APICombination.Services;

public sealed class GitHubClient(HttpClient httpClient, IOptions<ApiCredentials> credentials) : IGitHubClient
{
    private readonly HttpClient httpClient = ConfigureClient(httpClient, credentials.Value.GitHubToken);

    public async Task<IReadOnlyList<GitHubIssue>> GetOpenIssuesAsync(
        RepositoryReference repository, CancellationToken cancellationToken = default)
    {
        var issues = await GetAsync<List<GitHubIssueResponse>>(
            $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/issues?state=open&per_page=100",
            cancellationToken);

        return issues
            .Where(issue => issue.PullRequest is null)
            .Select(ToModel)
            .ToArray();
    }

    public async Task<GitHubIssue> GetIssueAsync(
        RepositoryReference repository, int issueNumber, CancellationToken cancellationToken = default)
    {
        if (issueNumber < 1)
        {
            throw new ApiRequestException("Issue number must be greater than zero.");
        }

        var issue = await GetAsync<GitHubIssueResponse>(
            $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/issues/{issueNumber}",
            cancellationToken);
        return ToModel(issue);
    }

    private async Task<T> GetAsync<T>(string relativeUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(relativeUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = response.StatusCode == HttpStatusCode.Forbidden
                    ? "GitHub rejected the request, possibly because the unauthenticated API rate limit was reached."
                    : $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.";
                throw new ApiRequestException(detail, (int)response.StatusCode);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value ?? throw new ApiRequestException("GitHub returned an empty response.");
        }
        catch (HttpRequestException exception)
        {
            throw new ApiRequestException($"GitHub could not be reached: {exception.Message}", null);
        }
    }

    private static GitHubIssue ToModel(GitHubIssueResponse issue) => new(
        issue.Number,
        issue.Title,
        issue.Body,
        issue.HtmlUrl,
        issue.Labels.Select(label => new GitHubLabel(label.Name, label.Color)).ToArray(),
        issue.PullRequest is not null);

    private static HttpClient ConfigureClient(HttpClient client, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        return client;
    }

    private sealed class GitHubIssueResponse
    {
        [JsonPropertyName("number")] public int Number { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("labels")] public List<GitHubLabelResponse> Labels { get; set; } = [];
        [JsonPropertyName("pull_request")] public object? PullRequest { get; set; }
    }

    private sealed class GitHubLabelResponse
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("color")] public string? Color { get; set; }
    }
}
