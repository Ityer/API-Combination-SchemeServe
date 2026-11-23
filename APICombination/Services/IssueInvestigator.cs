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
            var searchResults = await stackExchangeClient.SearchQuestionsAsync(query, cancellationToken);
            foreach (var question in searchResults)
            {
                questionsById.TryAdd(question.QuestionId, question);
            }
        }

        var rankedQuestions = await RankQuestionsAsync(issue, questionsById.Values, cancellationToken);
        return new InvestigationResult(issue, rankedQuestions);
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

    private async Task<IReadOnlyList<StackOverflowQuestion>> RankQuestionsAsync(
        GitHubIssue issue,
        IEnumerable<StackOverflowQuestion> questions,
        CancellationToken cancellationToken)
    {
        var questionList = questions.ToArray();
        var answers = await stackExchangeClient.GetTopAnswerExcerptsAsync(
            questionList.Where(question => question.IsAnswered).Select(question => question.QuestionId).ToArray(),
            cancellationToken);
        var keywordExtractor = new KeywordExtractor();
        var issueTitleKeywords = keywordExtractor.Extract(IssueTitleFormatter.RemoveLeadingIssueNumber(issue.Title), 4);
        var issueBodyKeywords = keywordExtractor.Extract(issue.Body, 6);
        var issueLabelKeywords = issue.Labels
            .SelectMany(label => keywordExtractor.Extract(label.Name, 2))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return questionList
            .Select(question =>
            {
                answers.TryGetValue(question.QuestionId, out var answer);
                var score = CalculateRelevanceScore(issueTitleKeywords, issueBodyKeywords, issueLabelKeywords, question, answer);
                return question with { RelevanceScore = score, TopAnswerExcerpt = answer };
            })
            .OrderByDescending(question => question.RelevanceScore)
            .ThenByDescending(question => question.IsAnswered)
            .ThenByDescending(question => question.Score)
            .ToArray();
    }

    public static int CalculateRelevanceScore(
        IReadOnlyList<string> issueTitleKeywords,
        IReadOnlyList<string> issueBodyKeywords,
        IReadOnlyList<string> issueLabelKeywords,
        StackOverflowQuestion question,
        string? answer)
    {
        var allKeywords = issueTitleKeywords
            .Concat(issueBodyKeywords)
            .Concat(issueLabelKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allKeywords.Length == 0)
        {
            return 0;
        }

        var titleMatches = CountMatches(issueTitleKeywords, question.Title);
        var labelMatches = CountMatches(issueLabelKeywords, string.Join(' ', question.Tags));
        var bodyMatches = CountMatches(issueBodyKeywords, $"{question.Title} {answer}");
        var weightedPoints = titleMatches * 18d + labelMatches * 14d + bodyMatches * 5d;
        var maximumPoints = issueTitleKeywords.Count * 18d + issueLabelKeywords.Count * 14d + issueBodyKeywords.Count * 5d;
        var score = maximumPoints == 0 ? 0 : (int)Math.Round(weightedPoints * 100d / maximumPoints);

        return Math.Min(100, score);
    }

    private static int CountMatches(IEnumerable<string> keywords, string text) =>
        keywords.Count(keyword => ContainsTerm(text, keyword));

    private static bool ContainsTerm(string text, string keyword)
    {
        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return keyword.EndsWith('s') && keyword.Length > 3
            ? text.Contains(keyword[..^1], StringComparison.OrdinalIgnoreCase)
            : text.Contains($"{keyword}s", StringComparison.OrdinalIgnoreCase);
    }
}
