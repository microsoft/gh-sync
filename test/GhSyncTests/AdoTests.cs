using Xunit;
using gh_sync;
using Octokit;
using System;
using System.Threading.Tasks;
using Moq;


public class AdoTests
{
    [Fact]
    public async Task GivenNullIssueThrowException()
    {
        Issue? testIssue = null;
        Func<Task> result = async () => await Ado.PullWorkItemFromIssue(testIssue);
        await Assert.ThrowsAsync<ArgumentNullException>(result);
    }

    [Fact]
    public async Task GivenIssueNoRepositoryThrowException()
    {
        Issue testIssue = new Issue();
        Func<Task> result = async () => await Ado.PullWorkItemFromIssue(testIssue);
        await Assert.ThrowsAsync<Exception>(result);
    }
}