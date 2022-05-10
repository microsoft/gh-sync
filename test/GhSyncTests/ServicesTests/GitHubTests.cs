// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync.Tests;

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
}