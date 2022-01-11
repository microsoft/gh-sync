using Octokit;

namespace gh_sync;

static class GitHub
{
    private const string GHPatName = "gh-pat";

    private static GitHubClient? ghClient = null;
    internal static async Task<GitHubClient> GetClient()
    {
        if (ghClient != null)
        {
            return ghClient;
        }

        while (true)
        {
            var pat = Extensions.RetreiveOrPrompt(
                GHPatName,
                prompt: "Please provide a PAT for use with GitHub: "
            );
            var tokenAuth = new Credentials(pat);
            try
            {
                ghClient = new GitHubClient(new ProductHeaderValue("ms-quantum-gh-sync"))
                {
                    Credentials = tokenAuth
                };
                var currentUser = await ghClient.User.Current();
                if (currentUser is User user && !string.IsNullOrWhiteSpace(user.Login))
                {
                    AnsiConsole.MarkupLine($"Using GitHub as {user.Login}.");
                    return ghClient;
                }
                else
                {
                    // Invalidate credential on failure.
                    Extensions.Invalidate(GHPatName);
                    AnsiConsole.MarkupLine($"No error authenticating to GitHub, but user was null.");
                }
            }
            catch (Exception ex)
            {
                // Invalidate credential on failure.
                Extensions.Invalidate(GHPatName);
                AnsiConsole.MarkupLine($"Error authenticating to GitHub.");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }
    }

    internal static Task<TResult> WithClient<TResult>(Func<GitHubClient, Task<TResult>> continuation) =>
        GetClient().Bind(continuation);
}
