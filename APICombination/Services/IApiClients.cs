namespace APICombination.Services;

public interface IGitHubClient
{
    Task<IReadOnlyList<GitHubIssue>> GetOpenIssuesAsync(RepositoryReference repository, CancellationToken cancellationToken = default);
    Task<GitHubIssue> GetIssueAsync(RepositoryReference repository, int issueNumber, CancellationToken cancellationToken = default);
}

public interface IStackExchangeClient
{
    Task<IReadOnlyList<StackOverflowQuestion>> SearchQuestionsAsync(string query, CancellationToken cancellationToken = default);
}
