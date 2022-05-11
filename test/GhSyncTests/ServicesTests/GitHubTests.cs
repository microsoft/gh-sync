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
    public void GetClientReturnsClientIfNotNull()
    {
        IGitHubClient? ghClient = new GitHubClient(new ProductHeaderValue("some-product-name")){
            Credentials = new Credentials("some-credentials")
        };

        Assert.NotNull(GitHub.GetClient("some-token", ghClient));
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
    public async void GetClientThorwsExceptionGivenInvalidCredentials()
    {
        IGitHubClient? nullClient = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await GitHub.GetClient("invalid-token", nullClient)
        );
    }

    [Fact]
    public async void GetClientReturnsClientGivenValidCredentials()
    {
        IGitHubClient? nullClient = null;
        var client = await GitHub.GetClient("valid-token", nullClient);
        Assert.NotNull(client);
    }
}