using System.Text.RegularExpressions;

namespace APICombination.Services;

public static partial class IssueTitleFormatter
{
    public static string RemoveLeadingIssueNumber(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        return LeadingIssueNumberRegex().Replace(title, string.Empty).TrimStart();
    }

    [GeneratedRegex("^\\s*#\\d+\\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingIssueNumberRegex();
}
