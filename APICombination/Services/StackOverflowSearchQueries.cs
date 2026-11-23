namespace APICombination.Services;

public sealed record StackOverflowSearchQueries(
    string Title,
    string Description,
    string Labels)
{
    public IEnumerable<string> NonEmptyQueries =>
        new[] { Title, Description, Labels }
            .Where(query => !string.IsNullOrWhiteSpace(query));
}
