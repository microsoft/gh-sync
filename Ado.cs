using System.Diagnostics;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Octokit;

namespace gh_sync;

static class Ado
{
    internal const string ADOTokenName = "ado-token";
    private const string collectionUri = "https://ms-quantum.visualstudio.com";

    internal const string ProjectName = "Quantum Program";

    private static VssConnection? adoConnection = null;
    private static async Task<VssConnection> GetAdoConnection()
    {
        if (adoConnection != null)
        {
            return adoConnection;
        }

        while (true)
        {
            var ADOToken = Extensions.RetreiveOrPrompt(
                ADOTokenName,
                prompt: "Please provide a PAT for use with Azure DevOps: ",
                envVarName: "ADO_TOKEN"
            );
            var creds = new VssBasicCredential(string.Empty, ADOToken);
            var connection = new VssConnection(new Uri(collectionUri), creds);
            try
            {
                await connection.ConnectAsync();
                adoConnection = connection;
                return connection;
            } 
            catch (Exception ex)
            {
                // Invalidate credential on failure.
                Extensions.Invalidate(ADOTokenName);
                AnsiConsole.MarkupLine($"Error authenticating to ADO.");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }
    }

    internal static Task<TResult> WithWorkItemClient<TResult>(Func<WorkItemTrackingHttpClient, Task<TResult>> continuation) =>
        GetAdoConnection().Bind(connection => connection.GetClient<WorkItemTrackingHttpClient>()).Bind(continuation);

    internal static async IAsyncEnumerable<Comment> EnumerateComments(this WorkItem workItem)
    {
        Debug.Assert(workItem.Id != null);

        string? continuationToken = null;
        do
        {
            var batch = await Ado.WithWorkItemClient(async client =>
                await client.GetCommentsAsync(
                    Ado.ProjectName,
                    workItem.Id.Value,
                    continuationToken: continuationToken
                )
            );
            foreach (var comment in batch.Comments)
            {
                yield return comment;
            }
            continuationToken = batch.ContinuationToken;
        }
        while (continuationToken != null);
    }

    internal static async IAsyncEnumerable<Comment> UpdateCommentsFromIssue(this WorkItem workItem, Issue issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        Debug.Assert(workItem.Id != null);

        var ghComments = await GitHub.WithClient(async client =>
            await client.Issue.Comment.GetAllForIssue(issue.Repository.Id, issue.Number)
        );
        var adoComments = await workItem.EnumerateComments().ToListAsync();

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
                        Ado.ProjectName,
                        workItem.Id.Value
                    )
                );
            }
        }
    }

    internal static async Task<WorkItem> PullWorkItemFromIssue(Issue issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        Debug.Assert(issue.Repository != null);

        var patch = issue.AsPatch();

        var newItem = await Ado.WithWorkItemClient(async client =>
            await client.CreateWorkItemAsync(
                patch,
                Ado.ProjectName,
                issue.WorkItemType()
            )
        );
        // newItem.

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
                Ado.ProjectName,
                newItem.Id.Value
            )
        );

        var nCommentsAdded = await newItem.UpdateCommentsFromIssue(issue).CountAsync();
        AnsiConsole.MarkupLine($"Added {nCommentsAdded} comments from GitHub issue.");
        return newItem;
    }

    internal static async Task<WorkItem> UpdateFromIssue(this WorkItem workItem, Issue issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        Debug.Assert(issue.Repository != null);
        Debug.Assert(workItem.Id != null);

        var patch = issue.AsPatch(operation: Operation.Replace);

        var result = await Ado.WithWorkItemClient(async client =>
            await client.UpdateWorkItemAsync(
                patch, workItem.Id.Value
            )
        );

        await Ado.WithWorkItemClient(async client =>
            await client.AddCommentAsync(
                new CommentCreate
                {
                    Text = $"Work item updated from GitHub public issue at {issue.Url}, using the gh-sync tool."
                },
                Ado.ProjectName,
                workItem.Id.Value
            )
        );

        return result;
    }
}
