// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Octokit;

namespace Microsoft.GhSync;

public record class Ado(IOptions options) : IAdo
{
    internal const string ADOTokenName = "ado-token";
    internal const string ADOUriName = "ado-uri";
    internal const string AdoProjectName = "ado-project";

    private static VssConnection? adoConnection = null;
    public async Task<VssConnection> GetAdoConnection(string ADOTokenName, VssConnection? adoConnection)
    {
        if (adoConnection != null)
        {
            return adoConnection;
        }

        var adoToken = options.GetToken(ADOTokenName);

        var connection = options.GetVssConnection(adoToken);

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
        GetAdoConnection(ADOTokenName, adoConnection).Bind(connection => connection.GetClient<WorkItemTrackingHttpClient>()).Bind(continuation);


    public async IAsyncEnumerable<Comment> EnumerateComments(WorkItem workItem)
    {
        if (workItem.Id == null)
        {
            throw new NullReferenceException($"New work item {workItem.Url} did not have an ID; could not add comment text.");
        }

        string? continuationToken = null;
        var projectName = options.GetToken(AdoProjectName);

        do
        {
            var batch = await WithWorkItemClient(async client =>
                await client.GetCommentsAsync(
                    Options._ProjectName,
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
