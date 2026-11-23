namespace APICombination.Services;

public sealed class IssueInvestigator(
    IGitHubClient gitHubClient,
    IStackExchangeClient stackExchangeClient,
    IRepositoryReferenceParser repositoryReferenceParser,
    IStackOverflowQueryBuilder queryBuilder) : IIssueInvestigator
{
    public async Task<IReadOnlyList<GitHubIssue>> GetOpenIssuesAsync(
        string repositoryUrl, CancellationToken cancellationToken = default)
    {
        if (!repositoryReferenceParser.TryParse(repositoryUrl, out var repository, out _, out var error))
        {
            throw new ApiRequestException(error ?? "The GitHub URL is invalid.");
        }

        var issues = await gitHubClient.GetOpenIssuesAsync(repository!, cancellationToken);
        return issues;
    }

    public async Task<InvestigationResult> InvestigateAsync(
        string repositoryUrl, int issueNumber, CancellationToken cancellationToken = default)
    {
        if (!repositoryReferenceParser.TryParse(repositoryUrl, out var repository, out _, out var error))
        {
            throw new ApiRequestException(error ?? "The GitHub URL is invalid.");
        }

        var issue = await gitHubClient.GetIssueAsync(repository!, issueNumber, cancellationToken);
        var queries = queryBuilder.Build(issue);
        var questionsById = new Dictionary<int, StackOverflowQuestion>();

        foreach (var query in queries.NonEmptyQueries)
        {
            var questions = await stackExchangeClient.SearchQuestionsAsync(query, cancellationToken);
            foreach (var question in questions)
            {
                questionsById.TryAdd(question.QuestionId, question);
            }
        }

        return new InvestigationResult(issue, questionsById.Values.ToArray());
    }

    public Task<IReadOnlyList<StackOverflowQuestion>> SearchStackOverflowAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<StackOverflowQuestion>>([]);
        }

        return stackExchangeClient.SearchQuestionsAsync(query.Trim(), cancellationToken);
    }
}
