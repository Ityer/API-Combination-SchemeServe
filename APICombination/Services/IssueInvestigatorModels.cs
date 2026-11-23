namespace APICombination.Services;

public sealed record RepositoryReference(string Owner, string Name);

public sealed record GitHubLabel(string Name, string? Color);

public sealed record GitHubIssue(
    int Number,
    string Title,
    string? Body,
    string HtmlUrl,
    IReadOnlyList<GitHubLabel> Labels,
    bool IsPullRequest = false);

public sealed record StackOverflowQuestion(
    int QuestionId,
    string Title,
    string Link,
    int Score,
    int AnswerCount,
    bool IsAnswered,
    IReadOnlyList<string> Tags,
    int RelevanceScore = 0,
    string? TopAnswerExcerpt = null);

public sealed record InvestigationResult(
    GitHubIssue Issue,
    IReadOnlyList<StackOverflowQuestion> Questions);

public sealed class ApiRequestException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
