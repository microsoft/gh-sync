// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Octokit;

namespace gh_sync;

/// <summary>
///     Service for synchronizing from GitHub to ADO.
/// </summary>
public interface ISynchronizer
{

    Task PullGitHubIssue(IServiceProvider services, Issue ghIssue, bool dryRun = false, bool allowExisting = false);

    Task<WorkItem> UpdateState(WorkItem workItem, Issue issue);

}
