using System.CommandLine;
using Octokit;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.CommandLine.Invocation;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace gh_sync;

class Program
{

    internal record PullIssueOptions(bool DryRun, bool AllowExisting);
    internal const string TrackingLabel = "tracking";
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
            var ghIssue = await GetGitHubIssue(repo, issue);
            await PullGitHubIssue(ghIssue, options.DryRun, options.AllowExisting);
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
                    var ghIssues = (await GetGitHubIssuesFromRepo(repo)).ToList();
                    foreach (var issue in ghIssues)
                    {
                        ctx.Status($"Pulling {issue.Repository.Owner.Name}/{issue.Repository.Name}#{issue.Number}: {issue.Title.Replace("[", "[[").Replace("]", "]]")}...");
                        await PullGitHubIssue(issue, options.DryRun, options.AllowExisting);
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
            var ghIssue = await GetGitHubIssue(repo, issue);
            var workItem = await GetAdoWorkItem(ghIssue);
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

    static async Task<IEnumerable<Issue>> GetGitHubIssuesFromRepo(string repo)
    {
        var parts = repo.Split("/", 2);
        var repository = await GitHub.WithClient(async client => await client.Repository.Get(parts[0], parts[1]));
        var issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            Filter = IssueFilter.All,
        };
        issueRequest.Labels.Add(TrackingLabel);
        
        var issues = await GitHub.WithClient(async client => await client.Issue.GetAllForRepository(
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

    static async Task<Issue> GetGitHubIssue(string repo, int id)
    {
        var parts = repo.Split("/", 2);
        return await GitHub.WithClient(async client =>
        {
            AnsiConsole.MarkupLine("[grey]Got GitHub client.[/]");
            var repository = await client.Repository.Get(parts[0], parts[1]);
            AnsiConsole.MarkupLine($"[grey]Got repository: {repository.HtmlUrl}.[/]");
            var issue = await client.Issue.Get(repositoryId: repository.Id, id);
            AnsiConsole.MarkupLine($"[grey]Got issue: {issue.HtmlUrl}.[/]");
            issue.AddRepoMetadata(repository);
            return issue;
        });
    }

    static async Task<WorkItem?> GetAdoWorkItem(Issue issue)
    {
        var escapedTitle = issue
            .WorkItemTitle()
            .Replace("\\", @"\\")
            .Replace("\"", @"\""");
        try
        {
            var workItems = await Ado.WithWorkItemClient(async (client) =>
            {
                return await client.QueryByWiqlAsync(
                    new Wiql
                    {
                        Query = $@"
                            SELECT [System.Id]
                            FROM WorkItems
                            WHERE ([Title] = ""{escapedTitle}"")
                        "
                    }
                );
            });
            return workItems.WorkItems.SingleOrDefault() is {} workItemRef
                    ? await Ado.WithWorkItemClient(client => client.GetWorkItemAsync(workItemRef.Id))
                    : null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"Exception querying existing ADO items for \"{escapedTitle}\".");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return null;
        }
    }

    private static async Task PullGitHubIssue(Issue ghIssue, bool dryRun = false, bool allowExisting = false)
    {
        // Check if there's already a work item.
        var workItem = await GetAdoWorkItem(ghIssue);
        if (workItem != null)
        {
            AnsiConsole.MarkupLine($"Found existing work item: {workItem.ReadableLink()}");
            if (!allowExisting)
            {
                AnsiConsole.MarkupLine("Updating existing issue, since --allow-existing was not set.");
                if (!dryRun)
                {
                    await workItem.UpdateFromIssue(ghIssue);
                    AnsiConsole.MarkupLine("Updating issue state...");
                    await workItem.UpdateState(ghIssue);
                    var nCommentsAdded = await workItem.UpdateCommentsFromIssue(ghIssue).CountAsync();
                    AnsiConsole.MarkupLine($"Added {nCommentsAdded} comments from GitHub issue.");
                }
                else
                {
                    AnsiConsole.MarkupLine("Not updating new work item in ADO, as --dry-run was set.");
                }
                return;
            }
            AnsiConsole.MarkupLine("Creating new work item, since --allow-existing was set.");
        }
        // TODO: possibly update milestones, item type, etc.

        if (!dryRun)
        {
            var newWorkItem = await Ado.PullWorkItemFromIssue(ghIssue);
            await newWorkItem.UpdateState(ghIssue);
            AnsiConsole.MarkupLine($@"Created new work item: {newWorkItem.ReadableLink()}");
        }
        else
        {
            AnsiConsole.MarkupLine("Not creating new work item in ADO, as --dry-run was set.");
        }
    }
}
