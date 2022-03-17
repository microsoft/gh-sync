using Octokit;

namespace gh_sync;

static class GitHub
{
    private const string GHTokenName = "gh-token";

    private static GitHubClient? ghClient = null;
    internal static async Task<GitHubClient> GetClient()
    {
        if (ghClient != null)
        {
            return ghClient;
        }

        while (true)
        {
            var GHToken = Extensions.RetreiveOrPrompt(
                GHTokenName,
                prompt: "Please provide a PAT for use with GitHub: ",
                envVarName: "GITHUB_TOKEN"
            );
            var tokenAuth = new Credentials(GHToken);
            try
            {
                ghClient = new GitHubClient(new ProductHeaderValue("ms-quantum-gh-sync"))
                {
                    Credentials = tokenAuth
                };
                // Try to use the client for something trivial so as to prompt
                // a failure as early as possible.
                var orgProfile = await ghClient.Organization.Get("microsoft");
                if (orgProfile is Organization profile)
                {
                    AnsiConsole.WriteLine("[green]Got profile OK![/]");
                }
                else
                {
                    AnsiConsole.WriteLine("[yellow]Unable to fetch public profiles; something may have gone wrong with auth. Please check logs carefully.[/]");
                }
                // var currentUser = await ghClient.User.Current();
                // AnsiConsole.MarkupLine($"currentUser: {currentUser}.");
                // AnsiConsole.MarkupLine($"user.Login: {user.Login}.");
                // ghClient.
                // if (currentUser is User user && !string.IsNullOrWhiteSpace(user.Login))
                // {
                //     AnsiConsole.MarkupLine($"Using GitHub as {user.Login}.");
                //     return ghClient;
                // }
                // else
                // {
                //     // Invalidate credential on failure.
                //     Extensions.Invalidate(GHTokenName);
                //     AnsiConsole.MarkupLine($"No error authenticating to GitHub, but user was null.");
                // }
            }
            catch (Exception ex)
            {
                // Invalidate credential on failure.
                Extensions.Invalidate(GHTokenName);
                AnsiConsole.MarkupLine($"Error authenticating to GitHub.");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }
    }

    internal static Task<TResult> WithClient<TResult>(Func<GitHubClient, Task<TResult>> continuation) =>
        GetClient().Bind(continuation);
}
