// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Octokit;

namespace gh_sync;

public interface IGitHub
{
    Task<IGitHubClient> GetClient(string GHTokenName, IGitHubClient? ghClient);

    Task<TResult> WithClient<TResult>(Func<IGitHubClient, Task<TResult>> continuation);

    Task<IEnumerable<Issue>> GetGitHubIssuesFromRepo(string repo);

    Task<Issue> GetGitHubIssue(string repo, int issueId);

}
