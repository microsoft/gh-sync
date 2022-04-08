using Xunit;
using gh_sync;
using Octokit;
using System;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;


public class AdoTests
{
    [Fact]
    public async Task GivenNullIssueThrowException()
    {
        Issue? nullIssue = null;
        WorkItem testWorkItem = new WorkItem();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.PullWorkItemFromIssue(nullIssue)
        );

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, nullIssue)
        );

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.GetAdoWorkItem(nullIssue)
        );
    }

    [Fact]
    public async Task GivenNullWorkItemIdThrowException()
    {
        Issue? issue = new Issue();
        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = null;

        await Assert.ThrowsAsync<Exception>(
            async () => await Ado.UpdateFromIssue(testWorkItem, issue)
        );
    }

    [Fact]
    public async Task GivenIssueNoRepositoryThrowException()
    {
        var issue = new Issue();

        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = 0;

        await Assert.ThrowsAsync<Exception>(
            async () => await Ado.UpdateFromIssue(testWorkItem, issue)
        );
    }
}

