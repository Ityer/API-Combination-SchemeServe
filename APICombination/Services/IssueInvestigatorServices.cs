namespace APICombination.Services;

public interface IRepositoryReferenceParser
{
    bool TryParse(string? input, out RepositoryReference? repository, out int? issueNumber, out string? error);
}

public interface IStackOverflowQueryBuilder
{
    StackOverflowSearchQueries Build(GitHubIssue issue);
}

public interface IIssueInvestigator
{
    Task<IReadOnlyList<GitHubIssue>> GetOpenIssuesAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<InvestigationResult> InvestigateAsync(string repositoryUrl, int issueNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StackOverflowQuestion>> SearchStackOverflowAsync(string query, CancellationToken cancellationToken = default);
}
