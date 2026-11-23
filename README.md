# Issue Investigator

Issue Investigator is a small .NET 10 Blazor application that compares a public GitHub issue with potentially relevant Stack Overflow questions.

## Run it

1. Install the .NET 10 SDK.
2. From the repository root, run:

   ```powershell
   dotnet run --project APICombination/APICombination.csproj
   ```

3. Open the HTTPS URL printed by the application.
4. Enter a public GitHub repository URL, for example `https://github.com/dotnet/runtime`.
5. Select an open issue, or enter an issue URL such as `https://github.com/dotnet/runtime/issues/123`.

No API key, account, database, or paid service is required for the basic flow. Optional credentials can increase API quotas.

## Optional API credentials

Credentials are loaded from the `ApiCredentials` configuration section and are optional. If they are absent, the application uses anonymous API access.

Environment variables are the safest local or deployment option:

```powershell
$env:ApiCredentials__GitHubToken = "github_pat_..."
$env:ApiCredentials__StackExchangeKey = "your-stack-app-key"
dotnet run --project APICombination/APICombination.csproj
```

The equivalent configuration names are `ApiCredentials:GitHubToken` and `ApiCredentials:StackExchangeKey`. Do not commit actual tokens or keys to `appsettings.json`; use environment variables, a deployment secret store, or local .NET user-secrets instead.

The GitHub token is sent as a Bearer token. The Stack Exchange key is sent as the API's `key` query parameter. These credentials only improve access to public read endpoints; they do not add private-repository or user OAuth functionality.

## How it works

- GitHub's public REST API is used to list open issues and retrieve one issue.
- Pull requests are excluded from the open-issue choices because GitHub includes them in the issues endpoint response.
- The issue title, labels, and (when present) a shortened description are transformed into a Stack Exchange `/2.3/search/advanced` query for `stackoverflow`.
- Each investigation performs up to three sequential Stack Overflow searches: title, description, and labels. Results are combined and deduplicated by question ID.
- If those searches return no questions, the UI provides a custom Stack Overflow query field and replaces the empty result set with the custom search results.
- Title and description searches use a built-in keyword parser that removes URLs, Markdown noise, common stopwords, issue metadata, duplicates, and numeric-only tokens. Title searches are intentionally limited to two terms and description searches to eight terms to avoid over-constraining Stack Overflow's `q` search.
- The UI displays the issue body and labels beside question titles, links, scores, tags, and answer status.

Both APIs have request quotas. The Stack Exchange client makes at most one retry per search when it receives a rate-limit or backoff response, waits for the server-provided delay, and then surfaces repeated failures. An issue without a description skips that search and still uses its title and labels.

## Tests

The NUnit test project covers URL parsing and Stack Overflow query transformation without making network requests:

```powershell
dotnet test APICombination.Tests/APICombination.Tests.csproj
```
