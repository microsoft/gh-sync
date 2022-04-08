using System.CommandLine;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.CommandLine.Invocation;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace gh_sync;

class Program
{

    internal record PullIssueOptions(bool DryRun, bool AllowExisting);
    private static Command PullIssueCommand()
    {

        var command = new Command("pull-gh")
        {
            new Argument<string>(
                "repo",
                description: "GitHub repository to pull the issue from."
            ),

            new Argument<int>(
                "issue",
                description: "ID of the issue to pull into ADO."
            ),

            new Option(
                "--dry-run",
                description: "Don't actually pull the GitHub issue into ADO."
            ),

            new Option(
                "--allow-existing",
                description: "Allow pulling an issue into ADO, even when a work item or bug already exists for that issue."
            )
        };

        command.Handler = CommandHandler.Create<string, int, PullIssueOptions>(async (repo, issue, options) =>
        {
            AnsiConsole.MarkupLine($"Getting GitHub issue {repo}#{issue}...");
            var ghIssue = await GitHub.GetGitHubIssue(repo, issue);
            await GitHub.PullGitHubIssue(ghIssue, options.DryRun, options.AllowExisting);
        });

        return command;

    }

    internal record PullAllIssueOptions(bool DryRun, bool AllowExisting);
    private static Command PullAllIssuesCommand()
    {

        var command = new Command("pull-all-gh")
        {
            new Argument<string>(
                "repo",
                description: "GitHub repository to pull the issue from."
            ),

            new Option(
                "--dry-run",
                description: "Don't actually pull the GitHub issue into ADO."
            ),

            new Option(
                "--allow-existing",
                description: "Allow pulling an issue into ADO, even when a work item or bug already exists for that issue."
            )
        };

        command.Handler = CommandHandler.Create<string, PullAllIssueOptions>(async (repo, options) =>
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Aesthetic).StartAsync(
                $"Getting all GitHub issues from {repo}...", async ctx =>
                {
                    var ghIssues = (await GitHub.GetGitHubIssuesFromRepo(repo)).ToList();
                    foreach (var issue in ghIssues)
                    {
                        ctx.Status($"Pulling {issue.Repository.Owner.Name}/{issue.Repository.Name}#{issue.Number}: {issue.Title.Replace("[", "[[").Replace("]", "]]")}...");
                        await GitHub.PullGitHubIssue(issue, options.DryRun, options.AllowExisting);
                    }
                    AnsiConsole.MarkupLine($"Pulled {ghIssues.Count} issues from {repo} into ADO.");
                });
        });

        return command;

    }
    

    private static Command GetAdoWorkItemCommand()
    {

        var command = new Command("get-ado")
        {
            new Argument<int>("id")
        };

        command.Handler = CommandHandler.Create<int>(async (id) =>
        {
            var workItem = await Ado.WithWorkItemClient(async client =>
                await client.GetWorkItemAsync(Ado.ProjectName, id, expand: WorkItemExpand.Relations)
            );
            workItem.WriteToConsole();
        });

        return command;

    }

    private static Command FindAdoWorkItemCommand()
    {

        var command = new Command("find-ado")
        {

            new Argument<string>(
                "repo",
                description: "GitHub repository to find the issue from."
            ),

            new Argument<int>(
                "issue",
                description: "ID of the issue to find an ADO work item from."
            )

        };

        command.Handler = CommandHandler.Create<string, int>(async (repo, issue) =>
        {
            AnsiConsole.MarkupLine($"Getting GitHub issue {repo}#{issue}...");
            var ghIssue = await GitHub.GetGitHubIssue(repo, issue);
            var workItem = await Ado.GetAdoWorkItem(ghIssue);
            if (workItem != null)
            {
                AnsiConsole.MarkupLine($"Found existing work item: {workItem.ReadableLink()}");
            }
            else
            {
                AnsiConsole.MarkupLine($"No ADO work item found for {repo}#{issue}.");
            }
        });

        return command;

    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            PullIssueCommand(),
            PullAllIssuesCommand(),
            GetAdoWorkItemCommand(),
            FindAdoWorkItemCommand()
        };
        var attachOption = new Option("--attach", "Attaches a debugger before running.");
        rootCommand.AddOption(attachOption);

        return await new CommandLineBuilder(rootCommand)
            .UseMiddleware(async (context, next) =>
            {
                var attach = context.ParseResult.HasOption(attachOption) && context.ParseResult.ValueForOption<bool>(attachOption);
                if (attach)
                {
                    System.Diagnostics.Debugger.Launch();
                    System.Diagnostics.Debugger.Break();
                }
                await next(context);
            })
            .UseDefaults()
            .Build()
            .InvokeAsync(args);
    }
}
