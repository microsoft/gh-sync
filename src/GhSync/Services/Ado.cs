using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Octokit;

namespace gh_sync;

public class Ado : IAdo
{
    internal const string ADOTokenName = "ado-token";
    internal const string ADOUriName = "ado-uri";
    internal const string AdoProjectName = "ado-project";

    internal static readonly string _CollectionUri = Extensions.RetreiveOrPrompt(
        ADOUriName,
        prompt: "Please provide a URI for your ADO project organization: ",
        envVarName: "ADO_URL"
    );
    internal static readonly string _ProjectName = Extensions.RetreiveOrPrompt(
        AdoProjectName,
        prompt: "Please provide a name for your ADO project: ",
        envVarName: "ADO_PROJECT"
    );

    private static VssConnection? adoConnection = null;
    public async Task<VssConnection> GetAdoConnection()
    {
        if (adoConnection != null)
        {
            return adoConnection;
        }

        var _ADOToken = Extensions.RetreiveOrPrompt(
            ADOTokenName,
            prompt: "Please provide a PAT for use with Azure DevOps: ",
            envVarName: "ADO_TOKEN"
        );

        var creds = new VssBasicCredential(string.Empty, _ADOToken);
        var connection = new VssConnection(new Uri(_CollectionUri), creds);
        
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

        throw new AuthorizationException();
    }

    public Task<TResult> WithWorkItemClient<TResult>(Func<WorkItemTrackingHttpClient, Task<TResult>> continuation) =>
        GetAdoConnection().Bind(connection => connection.GetClient<WorkItemTrackingHttpClient>()).Bind(continuation);


    public async IAsyncEnumerable<Comment> EnumerateComments(WorkItem workItem)
    {
        if (workItem.Id == null)
        {
            throw new NullReferenceException($"New work item {workItem.Url} did not have an ID; could not add comment text.");
        }

        string? continuationToken = null;
        do
        {
            var batch = await WithWorkItemClient(async client =>
                await client.GetCommentsAsync(
                    _ProjectName,
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
        var adoComments = await EnumerateComments(workItem).ToListAsync();

        foreach (var ghComment in ghComments)
        {
            var sigilString = $"[gh-sync: {ghComment.HtmlUrl}]";
            if (!adoComments.Any(comment => comment.Text.Contains(sigilString)))
            {
                // Need to post a new comment.
                var commentText = $"<a href=\"{ghComment.HtmlUrl}\">GitHub comment by @{ghComment.User.Login}:</a>\n\n{ghComment.Body.MarkdownToHtml()}\n\n<br><hr><span style=\"font-size: 8px\">{sigilString}</span>";
                yield return await WithWorkItemClient(async client =>
                    await client.AddCommentAsync(
                        new CommentCreate
                        {
                            Text = commentText
                        },
                        _ProjectName,
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

        var newItem = await WithWorkItemClient(async client =>
            {
                var result = await client.CreateWorkItemAsync(
                    patch,
                    _ProjectName,
                    issue.WorkItemType()
                );
                return result;
            }
        );

        if (newItem.Id == null)
        {
            throw new Exception($"New work item {newItem.Url} did not have an ID; could not add comment text.");
        }

        await WithWorkItemClient(async (client) =>
            await client.AddCommentAsync(
                new CommentCreate
                {
                    Text = $"Work item created from public GitHub issue at {issue.Url}, using the gh-sync tool."
                },
                _ProjectName,
                newItem.Id.Value
            )
        );

        var nCommentsAdded = await UpdateCommentsFromIssue(newItem, issue).CountAsync();
        AnsiConsole.MarkupLine($"Added {nCommentsAdded} comments from GitHub issue.");
        return newItem;
    }

    public async Task<WorkItem> UpdateFromIssue(WorkItem workItem, Issue? issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        if (issue.Repository == null) throw new NullReferenceException($"Issue {issue.Title} did not have an associated repository.");
        if (workItem.Id == null)
        {
            throw new NullReferenceException($"New work item {workItem.Url} did not have an ID; could not add comment text.");
        }

        var patch = issue.AsPatch(operation: Operation.Replace);

        var result = await WithWorkItemClient(async client =>
            await client.UpdateWorkItemAsync(
                patch, workItem.Id.Value
            )
        );

        return result;
    }

    public async Task<WorkItem?> GetAdoWorkItem(Issue? issue)
    {
        if (issue == null) throw new ArgumentNullException(nameof(issue));
        var escapedTitle = issue
            .WorkItemTitle()
            .Replace("\\", @"\\")
            .Replace("\"", @"\""");
        try
        {
            var workItems = await WithWorkItemClient(async (client) =>
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
                    ? await WithWorkItemClient(client => client.GetWorkItemAsync(workItemRef.Id))
                    : null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"Exception querying existing ADO items for \"{escapedTitle}\".");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            throw ex;
        }
    }
}
