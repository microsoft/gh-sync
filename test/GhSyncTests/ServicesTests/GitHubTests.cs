// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync.Tests;

using gh_sync;
using System.IO;
using Spectre.Console;
using Octokit;

public record class GitHubTests(MockStartup Startup) : IClassFixture<MockStartup>
{
    private Lazy<IGitHub> githubLazy = new Lazy<IGitHub>(
        () => ActivatorUtilities.CreateInstance<GitHub>(Startup.Services)
    );
    private IGitHub GitHub => githubLazy.Value;

    [Fact]
    public void CanCreateGitHubFromMocks()
    {
        Assert.NotNull(GitHub);
    }

    [Fact]
    public async Task GetClientThrowsExceptionGivenBadToken()
    {
        await Assert.ThrowsAsync<AuthorizationException>(
            async () => await GitHub.GetClient()
        );
    }
}