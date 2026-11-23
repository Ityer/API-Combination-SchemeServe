using System.Text.RegularExpressions;

namespace APICombination.Services;

public sealed partial class StackOverflowQueryBuilder(KeywordExtractor keywordExtractor) : IStackOverflowQueryBuilder
{
    public StackOverflowSearchQueries Build(GitHubIssue issue)
    {
        var title = string.Join(" ", keywordExtractor.Extract(IssueTitleFormatter.RemoveLeadingIssueNumber(issue.Title), maxKeywords: 2));
        var description = string.Join(" ", keywordExtractor.Extract(issue.Body));
        var labels = string.Join(" ", issue.Labels.Select(label => label.Name).Where(name => !string.IsNullOrWhiteSpace(name)));

        return new StackOverflowSearchQueries(
            title,
            description,
            NormalizeLabels(labels));
    }

    private static string NormalizeLabels(string labels) =>
        Regex.Replace(labels, "[`*_>#\\[\\]()]+", " ").Trim();
}
