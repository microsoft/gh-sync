using System;
using System.CommandLine;
using Octokit;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using System.Diagnostics;
using System.CommandLine.Invocation;
using System.Reflection;
using System.Linq;

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
            System.Console.WriteLine($"Getting GitHub issue {repo}#{issue}...");
            var ghIssue = await GetGitHubIssue(repo, issue);

            
            // Check if there's already a work item.
            var workItem = await GetAdoWorkItem(ghIssue);
            if (workItem != null)
            {
                System.Console.WriteLine($"Found existing work item: {workItem.ReadableLink()}");
                if (!options.AllowExisting)
                {
                    System.Console.WriteLine("Updating existing issue, since --allow-existing was not set.");
                    if (!options.DryRun)
                    {
                        await workItem.UpdateFromIssue(ghIssue);
                        System.Console.WriteLine("Updating issue state...");
                        await workItem.UpdateState(ghIssue);
                    }
                    else
                    {
                        System.Console.WriteLine("Not updating new work item in ADO, as --dry-run was set.");
                    }
                    return;
                }
                System.Console.WriteLine("Creating new work item, since --allow-existing was set.");
            }
            // TODO: possibly update milestones, item type, etc.

            if (!options.DryRun)
            {
                var newWorkItem = await Ado.PullWorkItemFromIssue(ghIssue);
                await newWorkItem.UpdateState(ghIssue);
                System.Console.WriteLine($@"Created new work item: {newWorkItem.ReadableLink()}");
            }
            else
            {
                System.Console.WriteLine("Not creating new work item in ADO, as --dry-run was set.");
            }
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
            System.Console.WriteLine($"Getting GitHub issue {repo}#{issue}...");
            var ghIssue = await GetGitHubIssue(repo, issue);
            var workItem = await GetAdoWorkItem(ghIssue);
            if (workItem != null)
            {
                System.Console.WriteLine($"Found existing work item: {workItem.ReadableLink()}");
            }
            else
            {
                System.Console.WriteLine($"No ADO work item found for {repo}#{issue}.");
            }
        });

        return command;

    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            PullIssueCommand(),
            GetAdoWorkItemCommand(),
            FindAdoWorkItemCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<Issue> GetGitHubIssue(string repo, int id)
    {
        var parts = repo.Split("/", 2);
        var repository = await GitHub.WithClient(async client => await client.Repository.Get(parts[0], parts[1]));
        var issue = await GitHub.WithClient(async client => await client.Issue.Get(repositoryId: repository.Id, id));
        // Workaround for https://github.com/octokit/octokit.net/issues/1616.
        issue
            .GetType()
            .GetProperties(
                BindingFlags.Instance
                | BindingFlags.GetProperty
                | BindingFlags.SetProperty
                | BindingFlags.GetField
                | BindingFlags.SetField
                | BindingFlags.Public
                | BindingFlags.NonPublic
            )
            .Single(
                property => property.Name == "Repository"
            )
            .SetValue(issue, repository);
        return issue;
    }

    /// <summary>
    ///     Gets an existing ADO work item for the given GitHub issue if
    ///     it already exists.
    /// </summary>
    /// <returns>
    ///     An existing work item for the given GitHub issue if one exists,
    ///     and <c>null</c> otherwise.
    /// </returns>
    static async Task<WorkItem?> GetAdoWorkItem(Issue issue)
    {
        var workItems = await Ado.WithWorkItemClient(async (client) =>
            await client.QueryByWiqlAsync(
                new Wiql
                {
                    Query = $@"
                        SELECT [System.Id]
                        FROM WorkItems
                        WHERE ([Title] = '{issue.WorkItemTitle()}')
                    "
                }
            )
        );
        return workItems.WorkItems.SingleOrDefault() is {} workItemRef
                ? await Ado.WithWorkItemClient(client => client.GetWorkItemAsync(workItemRef.Id))
                : null;
    }

    // The following comments came from attempts to use WIQL to search for
    // artifact links.
    // static async Task OtherStuff()
    // {

    //     var creds = new VssBasicCredential(string.Empty, adoPat);
    //     var connection = new VssConnection(new Uri(collectionUri), creds);
    //     await connection.ConnectAsync();
    //     var workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
    //     var existingItem = await workItemClient.GetWorkItemAsync(projectName, 30677, expand: WorkItemExpand.Relations);
    //     var related = await workItemClient.QueryByWiqlAsync(
    //         new Wiql
    //         {
    //             Query = $@"
    //                 SELECT [System.Id]
    //                 FROM WorkItemLinks
    //                 WHERE ([Source].[System.Id] = '{existingItem.Id}')
    //             "
    //         },
    //         projectName
    //     );
    //     Debug.Assert(related != null);
    //     foreach (var rel in related.WorkItemRelations)
    //     {
    //         (await workItemClient.GetWorkItemAsync(projectName, rel.Target.Id)).WriteToConsole();
    //     }
    // }

    // connection.GetClient<
        
    // var newItem = await client.CreateWorkItemAsync(
    //     new JsonPatchDocument
    //     {
    //         new JsonPatchOperation
    //         {
    //             Operation = Operation.Add,
    //             Path = "/fields/System.Title",
    //             Value = "[TEST] please delete"
    //         },
    //         new JsonPatchOperation
    //         {
    //             Operation = Operation.Add,
    //             Path = "/fields/System.AreaPath",
    //             Value = @"Quantum Program\Quantum Systems\QDK"
    //         }
    //     },
    //     projectName,
    //     "Task"
    // );
    // System.Console.WriteLine($"Created new work item: {newItem.Url}");
}
