namespace APICombination.Services;

public sealed class RepositoryReferenceParser : IRepositoryReferenceParser
{
    public bool TryParse(string? input, out RepositoryReference? repository, out int? issueNumber, out string? error)
    {
        repository = null;
        issueNumber = null;
        error = null;

        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Enter a full HTTPS GitHub URL such as https://github.com/dotnet/runtime.";
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is < 2 or > 4 || string.Equals(segments[1], ".git", StringComparison.OrdinalIgnoreCase))
        {
            error = "The URL must contain a GitHub owner and repository name.";
            return false;
        }

        var repositoryName = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        if (string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(repositoryName))
        {
            error = "The GitHub owner and repository name cannot be empty.";
            return false;
        }

        repository = new RepositoryReference(segments[0], repositoryName);
        if (segments.Length == 2)
        {
            return true;
        }

        if (!string.Equals(segments[2], "issues", StringComparison.OrdinalIgnoreCase) ||
            segments.Length != 4 || !int.TryParse(segments[3], out var parsedIssue) || parsedIssue < 1)
        {
            repository = null;
            error = "An optional issue must use the format /issues/123.";
            return false;
        }

        issueNumber = parsedIssue;
        return true;
    }
}
