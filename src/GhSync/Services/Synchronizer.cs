using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Octokit;

namespace gh_sync;

public record class Synchronizer(IAdo Ado, IGitHub GitHub) : ISynchronizer
{
    public async Task PullGitHubIssue(Issue ghIssue, bool dryRun = false, bool allowExisting = false)
    {
        // Check if there's already a work item.
        var workItem = await Ado.GetAdoWorkItem(ghIssue);
        if (workItem != null)
        {
            AnsiConsole.MarkupLine($"Found existing work item: {workItem.ReadableLink()}");
            if (!allowExisting)
            {
                AnsiConsole.MarkupLine("Updating existing issue, since --allow-existing was not set.");
                if (!dryRun)
                {
                    await Ado.UpdateFromIssue(workItem, ghIssue);
                    AnsiConsole.MarkupLine("Updating issue state...");
                    await UpdateState(workItem, ghIssue);
                    var nCommentsAdded = await Ado.UpdateCommentsFromIssue(workItem, ghIssue).CountAsync();
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
            await UpdateState(newWorkItem, ghIssue);
            AnsiConsole.MarkupLine($@"Created new work item: {newWorkItem.ReadableLink()}");
        }
        else
        {
            AnsiConsole.MarkupLine("Not creating new work item in ADO, as --dry-run was set.");
        }
    }

    public async Task<WorkItem> UpdateState(WorkItem workItem, Issue issue) =>
        await Ado.WithWorkItemClient(async client =>
        {
            if (issue.WorkItemState() is {} state)
            {
                return await client.UpdateWorkItemAsync(
                    new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
                    {
                        new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                            Path = "/fields/System.State",
                            Value = state.State
                        }
                    },
                    gh_sync.Ado.ProjectName, workItem.Id!.Value
                );
                // TODO: update Reason
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold yellow]Status of work item {workItem.ReadableLink()} not updated, as GitHub issue {issue.HtmlUrl} may be missing a triage label.[/]");
                return workItem;
            }
        });
}
