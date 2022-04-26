// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Octokit;

namespace gh_sync;

public interface IGitHub
{
    Task<GitHubClient> GetClient();

    Task<TResult> WithClient<TResult>(Func<GitHubClient, Task<TResult>> continuation);

    Task<IEnumerable<Issue>> GetGitHubIssuesFromRepo(string repo);

    Task<Issue> GetGitHubIssue(string repo, int issueId);

    Task PullAllIssues(IServiceProvider services, string repo, bool dryRun, bool allowExisting);
}
