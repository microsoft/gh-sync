using Xunit;
using gh_sync;
using Octokit;
using System;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;


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
        Mock<Issue> issue = new Mock<Issue>();
        issue.Setup(x => x.Repository).Returns(new Repository());

        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = 0;

        Assert.NotNull(issue);
        Assert.NotNull(issue.Object.Repository);
        Assert.NotNull(testWorkItem.Id);

        await Assert.ThrowsAsync<Exception>(
            async () => await Ado.UpdateFromIssue(testWorkItem, issue.Object)
        );
    }
}