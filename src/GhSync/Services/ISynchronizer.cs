using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Octokit;

namespace gh_sync;

/// <summary>
///     Service for synchronizing from GitHub to ADO.
/// </summary>
public interface ISynchronizer
{

    Task PullGitHubIssue(Issue ghIssue, bool dryRun = false, bool allowExisting = false);

    Task<WorkItem> UpdateState(WorkItem workItem, Issue issue);

}
