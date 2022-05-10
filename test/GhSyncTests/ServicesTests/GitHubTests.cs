// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync.Tests;

using gh_sync;
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
    public async Task GetClientThrowsExceptionsGivenBadToken()
    {
        IGitHubClient? nullClient = null;

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await GitHub.GetClient("bad-token", nullClient)
        );
    }

    [Fact]
    public async Task GetClientThrowsExceptionsGivenUnauthorizedToken()
    {
        IGitHubClient? nullClient = null;

        await Assert.ThrowsAsync<AuthorizationException>(
            async () => await GitHub.GetClient("unauthorized-token", nullClient)
        );
    }

    [Fact]
    public void GetClientReturnsClientIfNotNull()
    {
        IGitHubClient? ghClient = new GitHubClient(new ProductHeaderValue("test-product-name"));

        Assert.NotNull(GitHub.GetClient("test-token", ghClient));
    }
}