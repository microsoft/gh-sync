// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace gh_sync;

class Program
{
    protected readonly IServiceProvider services;

    public Program(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        if (configureServices == null)
        {
            new Startup().ConfigureServices(services);
        }
        else
        {
            configureServices(services);
        }
        this.services = services.BuildServiceProvider();
    }

    static async Task<int> Main(string[] args) =>
        await new Program().Invoke(args);

    private Command PullIssueCommand(Argument<string> repo, Argument<int> issue, Option<bool> dryRun, Option<bool> allowExisting)
    {
        var command = new Command("pull-gh", "Pull from GitHub into ADO")
        {
            repo,
            issue,
            dryRun,
            allowExisting
        };

        command.SetHandler(async (string repo, int issueId, bool dryRun, bool allowExisting) =>
        {
            var sync = services.GetRequiredService<ISynchronizer>();
            var gh = services.GetRequiredService<IGitHub>();

            var ghIssue = await gh.GetGitHubIssue(repo, issueId);
            await sync.PullGitHubIssue(ghIssue, dryRun, allowExisting);
        }, repo, issue, dryRun, allowExisting);

        return command;
    }

    internal record PullAllIssueOptions(bool DryRun, bool AllowExisting);
    private Command PullAllIssuesCommand(Argument<string> repo, Option<bool> dryRun, Option<bool> allowExisting)
    {
        var command = new Command("pull-all-gh")
        {
            repo,
            dryRun,
            allowExisting
        };

        command.SetHandler(async (string repo, bool dryRun, bool allowExisting) =>
        {
            var sync = services.GetRequiredService<ISynchronizer>();
            await sync.PullAllIssues(repo, dryRun, allowExisting);
        }, repo, dryRun, allowExisting);

        return command;

    }

    private Command GetAdoWorkItemCommand(Argument<int> id)
    {
        var command = new Command("get-ado")
        {
            id
        };

        command.SetHandler<int>(async (id) =>
        {
            var ado = services.GetRequiredService<IAdo>();
            var workItem = await ado.WithWorkItemClient(async client =>
                await client.GetWorkItemAsync(Options._ProjectName, id, expand: WorkItemExpand.Relations)
            );
            workItem.WriteToConsole();
        }, id);

        return command;

    }

    private Command FindAdoWorkItemCommand(Argument<string> repo, Argument<int> issue)
    {
        var command = new Command("find-ado")
        {
            repo,
            issue
        };

        command.SetHandler<string, int>(async (repo, issue) =>
        {
            var ado = services.GetRequiredService<IAdo>();
            var gh = services.GetRequiredService<IGitHub>();

            var ghIssue = await gh.GetGitHubIssue(repo, issue);
            var workItem = await ado.GetAdoWorkItem(ghIssue);
            if (workItem != null)
            {
                AnsiConsole.MarkupLine($"Found existing work item: {workItem.ReadableLink()}");
            }
            else
            {
                AnsiConsole.MarkupLine($"No ADO work item found for {repo}#{issue}.");
            }
        }, repo, issue);

        return command;

    }

    async Task<int> Invoke(string[] args)
    {
        var id = new Argument<int>("id", "The id of the ADO work item.");
        var repo = new Argument<string>("repo", "GitHub repository to pull the issue from.");
        var issue = new Argument<int>("issue", "ID of the issue to pull into ADO.");
        var dryRun = new Option<bool>("--dry-run", "Don't actually pull the GitHub issue into ADO.");
        var allowExisting = new Option<bool>("--allow-existing", "Allow pulling an issue into ADO, even when a work item or bug already exists for that issue.");

        var rootCommand = new RootCommand
        {
            PullIssueCommand(repo, issue, dryRun, allowExisting),
            PullAllIssuesCommand(repo, dryRun, allowExisting),
            GetAdoWorkItemCommand(id),
            FindAdoWorkItemCommand(repo, issue)
        };

        var attachOption = new Option<bool>("--attach", "Attaches a debugger before running.");
        rootCommand.AddOption(attachOption);

        return await new CommandLineBuilder(rootCommand)
            .AddMiddleware(async (context, next) =>
            {
                var attach = context.ParseResult.HasOption(attachOption) && context.ParseResult.GetValueForOption<bool>(attachOption);
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
