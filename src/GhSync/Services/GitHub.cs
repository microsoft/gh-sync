// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Octokit;

namespace gh_sync;

public class GitHub : IGitHub
{
    private const string GHTokenName = "gh-token";
    private const string ProductName = "ms-quantum-gh-sync";
    private const string OrgName = "microsoft";
    internal const string TrackingLabel = "tracking";

    private static GitHubClient? ghClient = null;
    public async Task<GitHubClient> GetClient()
    {
        if (ghClient != null)
        {
            return ghClient;
        }

        var GHToken = Extensions.RetreiveOrPrompt(
            GHTokenName,
            prompt: "Please provide a PAT for use with GitHub: ",
            envVarName: "GITHUB_TOKEN"
        );

        var tokenAuth = new Credentials(GHToken);
        try
        {
            ghClient = new GitHubClient(new ProductHeaderValue(ProductName))
            {
                Credentials = tokenAuth
            };

            var orgProfile = await ghClient.Organization.Get(OrgName);
            if (orgProfile is Organization profile)
            {
                AnsiConsole.MarkupLine("[green]Got profile OK![/]");
                return ghClient;
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Unable to fetch public profiles; something may have gone wrong with auth. Please check logs carefully.[/]");
            }
        }
        catch (Exception ex)
        {
            // Invalidate credential on failure.
            Extensions.Invalidate(GHTokenName);
            AnsiConsole.MarkupLine($"Error authenticating to GitHub.");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }

        throw new AuthorizationException();
    }

    public Task<TResult> WithClient<TResult>(Func<GitHubClient, Task<TResult>> continuation) =>
        GetClient().Bind(continuation);

    public async Task<IEnumerable<Issue>> GetGitHubIssuesFromRepo(string repo)
    {
        var parts = repo.Split("/", 2);
        var repository = await WithClient(async client => await client.Repository.Get(parts[0], parts[1]));
        var issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            Filter = IssueFilter.All,
        };
        issueRequest.Labels.Add(TrackingLabel);
        
        var issues = await WithClient(async client => await client.Issue.GetAllForRepository(
            repositoryId: repository.Id,
            request: issueRequest
        ));
        return issues
            .Where(issue => issue.PullRequest == null)
            .Select(issue =>
            {
                var withRepo = issue.AddRepoMetadata(repository);
                return withRepo;
            });
    }

    public async Task<Issue> GetGitHubIssue(string repo, int issueId)
    {
        AnsiConsole.MarkupLine($"Getting GitHub issue {repo}#{issueId}...");
        var parts = repo.Split("/", 2);
        return await WithClient(async client =>
        {
            AnsiConsole.MarkupLine("[white]Got GitHub client.[/]");
            var repository = await client.Repository.Get(parts[0], parts[1]);
            AnsiConsole.MarkupLine($"[white]Got repository: {repository.HtmlUrl}.[/]");
            var issue = await client.Issue.Get(repositoryId: repository.Id, issueId);
            AnsiConsole.MarkupLine($"[white]Got issue: {issue.HtmlUrl}.[/]");
            issue.AddRepoMetadata(repository);
            return issue;
        });
    }

    public async Task PullAllIssues(IServiceProvider services, string repo, bool dryRun, bool allowExisting)
    {
        var sync = services.GetRequiredService<ISynchronizer>();
        await AnsiConsole.Status().Spinner(Spinner.Known.Aesthetic).StartAsync(
            $"Getting all GitHub issues from {repo}...", async ctx =>
            {
                var ghIssues = (await GetGitHubIssuesFromRepo(repo)).ToList();
                foreach (var issue in ghIssues)
                {
                    ctx.Status($"Pulling {issue.Repository.Owner.Name}/{issue.Repository.Name}#{issue.Number}: {issue.Title.Replace("[", "[[").Replace("]", "]]")}...");
                    await sync.PullGitHubIssue(services, issue, dryRun, allowExisting);
                }
                AnsiConsole.MarkupLine($"Pulled {ghIssues.Count} issues from {repo} into ADO.");
            }
        );
    }
}
