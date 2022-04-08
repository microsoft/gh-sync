using Xunit;
using gh_sync;
using System;
using System.Threading.Tasks;

public class GitHubTests
{
    [Fact]
    public async Task GivenBadRepositoryThrowException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await GitHub.GetGitHubIssuesFromRepo("testRepo")
        );

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await GitHub.GetGitHubIssue("testRepo", 0)
        );
    }
}