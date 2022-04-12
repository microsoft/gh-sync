using Octokit;

namespace gh_sync;

public class GitHub : IGitHub
{
    private const string GHTokenName = "gh-token";
    private const string ProductName = "ms-quantum-gh-sync";
    private const string OrgName = "microsoft";
    internal const string TrackingLabel = "tracking";

    private static GitHubClient? ghClient = null;
    internal static async Task<GitHubClient> GetClient()
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

    internal static Task<TResult> WithClient<TResult>(Func<GitHubClient, Task<TResult>> continuation) =>
        GetClient().Bind(continuation);

    public static async Task<IEnumerable<Issue>> GetGitHubIssuesFromRepo(string repo)
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

    public static async Task<Issue> GetGitHubIssue(string repo, int id)
    {
        var parts = repo.Split("/", 2);
        return await WithClient(async client =>
        {
            AnsiConsole.MarkupLine("[white]Got GitHub client.[/]");
            var repository = await client.Repository.Get(parts[0], parts[1]);
            AnsiConsole.MarkupLine($"[white]Got repository: {repository.HtmlUrl}.[/]");
            var issue = await client.Issue.Get(repositoryId: repository.Id, id);
            AnsiConsole.MarkupLine($"[white]Got issue: {issue.HtmlUrl}.[/]");
            issue.AddRepoMetadata(repository);
            return issue;
        });
    }

}
