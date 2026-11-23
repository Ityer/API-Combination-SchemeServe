using APICombination.Components;
using APICombination.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ApiCredentials>(builder.Configuration.GetSection("ApiCredentials"));
builder.Services.AddSingleton<KeywordExtractor>();

builder.Services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("IssueInvestigator/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});
builder.Services.AddHttpClient<IStackExchangeClient, StackExchangeClient>(client =>
{
    client.BaseAddress = new Uri("https://api.stackexchange.com/2.3/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("IssueInvestigator/1.0");
});
builder.Services.AddSingleton<IRepositoryReferenceParser, RepositoryReferenceParser>();
builder.Services.AddSingleton<IStackOverflowQueryBuilder, StackOverflowQueryBuilder>();
builder.Services.AddScoped<IIssueInvestigator, IssueInvestigator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
