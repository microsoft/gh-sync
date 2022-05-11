// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync;

using Octokit;

public interface IOptions
{
    string GetToken(string varName);

    Task<Organization?> GetOrgProfile(IGitHubClient ghClient, string orgName);
}