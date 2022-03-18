using Octokit;

namespace gh_sync;

static class GitHub
{
    private const string GHTokenName = "gh-token";
    private const string ProductName = "ms-quantum-gh-sync";
    private const string OrgName = "microsoft";

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
}
