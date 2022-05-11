// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
                    var nCommentsAdded = await UpdateCommentsFromIssue(workItem, ghIssue).CountAsync();
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
            var newWorkItem = await PullWorkItemFromIssue(ghIssue);
            await UpdateState(newWorkItem, ghIssue);
            AnsiConsole.MarkupLine($@"Created new work item: {newWorkItem.ReadableLink()}");
        }
        else
        {
            AnsiConsole.MarkupLine("Not creating new work item in ADO, as --dry-run was set.");
        }
    }

    public async Task<WorkItem> UpdateState(WorkItem workItem, Issue issue)
    {
        if (issue.WorkItemState() is {} state)
        {
            return await Ado.WithWorkItemClient(async client =>
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
                    Options._ProjectName, workItem.Id!.Value
                );
            });
            // TODO: update Reason
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold yellow]Status of work item {workItem.ReadableLink()} not updated, as GitHub issue {issue.HtmlUrl} may be missing a triage label.[/]");
            return workItem;
        }
    }

    public async IAsyncEnumerable<Comment> UpdateCommentsFromIssue(WorkItem workItem, Issue? issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        if (workItem.Id == null)
        {
            throw new NullReferenceException($"New work item {workItem.Url} did not have an ID; could not add comment text.");
        }
        var ghComments = await GitHub.WithClient(async client =>
            await client.Issue.Comment.GetAllForIssue(issue.Repository.Id, issue.Number)
        );
        var adoComments = await Ado.EnumerateComments(workItem).ToListAsync();

        foreach (var ghComment in ghComments)
        {
            var sigilString = $"[gh-sync: {ghComment.HtmlUrl}]";
            if (!adoComments.Any(comment => comment.Text.Contains(sigilString)))
            {
                // Need to post a new comment.
                var commentText = $"<a href=\"{ghComment.HtmlUrl}\">GitHub comment by @{ghComment.User.Login}:</a>\n\n{ghComment.Body.MarkdownToHtml()}\n\n<br><hr><span style=\"font-size: 8px\">{sigilString}</span>";
                yield return await Ado.WithWorkItemClient(async client =>
                    await client.AddCommentAsync(
                        new CommentCreate
                        {
                            Text = commentText
                        },
                        Options._ProjectName,
                        workItem.Id.Value
                    )
                );
            }
        }
    }

    public async Task<WorkItem> PullWorkItemFromIssue(Issue? issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        if (issue.Repository == null) throw new NullReferenceException($"Issue {issue.Title} did not have an associated repository.");
        
        var patch = issue.AsPatch();

        var newItem = await Ado.WithWorkItemClient(async client =>
            {
                var result = await client.CreateWorkItemAsync(
                    patch,
                    Options._ProjectName,
                    issue.WorkItemType()
                );
                return result;
            }
        );

        if (newItem.Id == null)
        {
            throw new Exception($"New work item {newItem.Url} did not have an ID; could not add comment text.");
        }

        await Ado.WithWorkItemClient(async (client) =>
            await client.AddCommentAsync(
                new CommentCreate
                {
                    Text = $"Work item created from public GitHub issue at {issue.Url}, using the gh-sync tool."
                },
                Options._ProjectName,
                newItem.Id.Value
            )
        );

        var nCommentsAdded = await UpdateCommentsFromIssue(newItem, issue).CountAsync();
        AnsiConsole.MarkupLine($"Added {nCommentsAdded} comments from GitHub issue.");
        return newItem;
    }
    

    public async Task PullAllIssues(string repo, bool dryRun, bool allowExisting)
    {
        await AnsiConsole.Status().Spinner(Spinner.Known.Aesthetic).StartAsync(
            $"Getting all GitHub issues from {repo}...", async ctx =>
            {
                var ghIssues = (await GitHub.GetGitHubIssuesFromRepo(repo)).ToList();
                foreach (var issue in ghIssues)
                {
                    ctx.Status($"Pulling {issue.Repository.Owner.Name}/{issue.Repository.Name}#{issue.Number}: {issue.Title.Replace("[", "[[").Replace("]", "]]")}...");
                    await PullGitHubIssue(issue, dryRun, allowExisting);
                }
                AnsiConsole.MarkupLine($"Pulled {ghIssues.Count} issues from {repo} into ADO.");
            }
        );
    }
}
