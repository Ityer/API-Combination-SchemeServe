using System.Net;
using System.Net.Http.Headers;
using APICombination.Services;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace APICombination.Tests;

public sealed class IssueInvestigatorTests
{
    private readonly RepositoryReferenceParser parser = new();
    private readonly StackOverflowQueryBuilder queryBuilder = new(new KeywordExtractor());

    [Test]
    public void TryParseRepositoryUrl_ReturnsOwnerAndRepository()
    {
        var result = parser.TryParse("https://github.com/dotnet/runtime", out var repository, out var issue, out var error);

        Assert.That(result, Is.True);
        Assert.That(repository, Is.EqualTo(new RepositoryReference("dotnet", "runtime")));
        Assert.That(issue, Is.Null);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void TryParseIssueUrl_ReturnsIssueNumber()
    {
        var result = parser.TryParse("https://github.com/dotnet/runtime/issues/123", out var repository, out var issue, out _);

        Assert.That(result, Is.True);
        Assert.That(repository, Is.EqualTo(new RepositoryReference("dotnet", "runtime")));
        Assert.That(issue, Is.EqualTo(123));
    }

    [Test]
    public void TryParseRejectsNonGithubUrl()
    {
        var result = parser.TryParse("https://example.com/dotnet/runtime", out _, out _, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("HTTPS GitHub URL"));
    }

    [Test]
    public void BuildSeparatesTitleDescriptionAndLabels()
    {
        var issue = new GitHubIssue(1, "Null reference in parser", "Fails on startup", "https://github.com/a/b/issues/1", [new GitHubLabel("bug", "red")]);

        var queries = queryBuilder.Build(issue);

        Assert.That(queries.Title, Is.EqualTo("Null reference"));
        Assert.That(queries.Description, Is.EqualTo("Fails startup"));
        Assert.That(queries.Labels, Is.EqualTo("bug"));
        Assert.That(queries.NonEmptyQueries.Count(), Is.EqualTo(3));
    }

    [Test]
    public void BuildWorksWithoutDescription()
    {
        var issue = new GitHubIssue(1, "Improve logging", null, "https://github.com/a/b/issues/1", []);

        var queries = queryBuilder.Build(issue);

        Assert.That(queries.Title, Is.EqualTo("Improve logging"));
        Assert.That(queries.Description, Is.Empty);
        Assert.That(queries.Labels, Is.Empty);
    }

    [Test]
    public void LeadingIssueNumberIsRemovedFromTitleSearchAndDisplayTitle()
    {
        var issue = new GitHubIssue(1, "#123 Fix the parser", null, "https://github.com/a/b/issues/1", []);

        var queries = queryBuilder.Build(issue);

        Assert.That(queries.Title, Is.EqualTo("Fix parser"));
        Assert.That(IssueTitleFormatter.RemoveLeadingIssueNumber(issue.Title), Is.EqualTo("Fix the parser"));
    }

    [Test]
    public void KeywordParserRemovesNoiseAndKeepsTechnicalTerms()
    {
        var extractor = new KeywordExtractor();

        var keywords = extractor.Extract("How do I fix **HttpClient** with C#? See https://example.com/issue/123.");

        Assert.That(keywords, Does.Contain("HttpClient"));
        Assert.That(keywords, Does.Contain("C#"));
        Assert.That(keywords, Does.Not.Contain("https"));
        Assert.That(keywords, Does.Not.Contain("123"));
    }

    [Test]
    public void KeywordParserLimitsResultsToEightUniqueTerms()
    {
        var keywords = new KeywordExtractor().Extract("alpha bravo charlie delta echo foxtrot golf hotel india juliet alpha");

        Assert.That(keywords.Count, Is.EqualTo(8));
        Assert.That(keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(8));
    }

    [Test]
    public void TitleSearchUsesAtMostTwoKeywords()
    {
        var issue = new GitHubIssue(1, "TestServer.CreateClient HttpClient created support cancellation", null, "https://github.com/a/b/issues/1", []);

        var queries = queryBuilder.Build(issue);

        Assert.That(queries.Title, Is.EqualTo("TestServer.CreateClient HttpClient"));
    }

    [Test]
    public async Task GitHubClientUsesConfiguredBearerToken()
    {
        var handler = new RecordingHandler("{\"number\":1,\"title\":\"Test\",\"html_url\":\"https://github.com/a/b/issues/1\",\"labels\":[]}");
        var client = new GitHubClient(CreateHttpClient(handler), Options.Create(new ApiCredentials { GitHubToken = "token-value" }));

        await client.GetIssueAsync(new RepositoryReference("a", "b"), 1);

        Assert.That(handler.LastRequest!.Headers.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "token-value")));
    }

    [Test]
    public async Task StackExchangeClientAddsConfiguredKey()
    {
        var handler = new RecordingHandler("{\"items\":[]}");
        var client = new StackExchangeClient(CreateHttpClient(handler), Options.Create(new ApiCredentials { StackExchangeKey = "stack-key" }));

        await client.SearchQuestionsAsync("test query");

        Assert.That(handler.LastRequest!.RequestUri!.Query, Does.Contain("key=stack-key"));
    }

    [Test]
    public async Task ClientsRemainAnonymousWithoutCredentials()
    {
        var handler = new RecordingHandler("{\"number\":1,\"title\":\"Test\",\"html_url\":\"https://github.com/a/b/issues/1\",\"labels\":[]}");
        var client = new GitHubClient(CreateHttpClient(handler), Options.Create(new ApiCredentials()));

        await client.GetIssueAsync(new RepositoryReference("a", "b"), 1);

        Assert.That(handler.LastRequest!.Headers.Authorization, Is.Null);
    }

    [Test]
    public async Task StackExchangeClientRetriesOnceAfterRateLimit()
    {
        var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var successful = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"items\":[]}")
        };
        var handler = new SequenceHandler(rateLimited, successful);
        var client = new StackExchangeClient(CreateHttpClient(handler), Options.Create(new ApiCredentials()));

        await client.SearchQuestionsAsync("test query");

        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://test.local/")
    };

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses[Math.Min(RequestCount - 1, responses.Length - 1)]);
        }
    }
}
