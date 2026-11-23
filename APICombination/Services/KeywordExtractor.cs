using System.Text.RegularExpressions;

namespace APICombination.Services;

public sealed partial class KeywordExtractor
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "against", "all", "also", "and", "any", "are", "because",
        "been", "before", "being", "but", "can", "could", "did", "does", "doing", "for",
        "from", "have", "having", "how", "into", "issue", "its", "just", "more", "most",
        "not", "now", "only", "other", "our", "out", "over", "same", "should", "some",
        "such", "than", "that", "the", "their", "them", "then", "there", "these", "they", "see",
        "this", "through", "too", "under", "very", "was", "were", "what", "when", "where",
        "which", "while", "with", "would", "you", "your", "github", "stackoverflow"
    };

    public IReadOnlyList<string> Extract(string? text, int maxKeywords = 8)
    {
        if (string.IsNullOrWhiteSpace(text) || maxKeywords < 1)
        {
            return [];
        }

        var withoutUrls = UrlRegex.Replace(text, " ");
        var keywords = new List<string>(maxKeywords);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TokenRegex.Matches(withoutUrls))
        {
            var token = match.Value.Trim('.', '-', '_');
            if ((token.Length < 3 && !token.Contains('#') && !token.Contains('.')) ||
                token.All(char.IsDigit) || StopWords.Contains(token) || !seen.Add(token))
            {
                continue;
            }

            keywords.Add(token);
            if (keywords.Count == maxKeywords)
            {
                break;
            }
        }

        return keywords;
    }

    private static readonly Regex UrlRegex = new(
        "https?://\\S+|www\\.\\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        "[A-Za-z][A-Za-z0-9+#.-]*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
