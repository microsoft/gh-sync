// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Octokit;

namespace gh_sync;

public interface IAdo
{
    Task<VssConnection> GetAdoConnection();

    Task<TResult> WithWorkItemClient<TResult>(Func<WorkItemTrackingHttpClient, Task<TResult>> continuation);

    IAsyncEnumerable<Comment> UpdateCommentsFromIssue(WorkItem workItem, Issue? issue);

    IAsyncEnumerable<Comment> EnumerateComments(WorkItem workItem);

    Task<WorkItem> PullWorkItemFromIssue(Issue? issue);

    Task<WorkItem> UpdateFromIssue(WorkItem workItem, Issue? issue);

    Task<WorkItem?> GetAdoWorkItem(Issue? issue);
}
