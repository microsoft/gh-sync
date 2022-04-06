using Xunit;
using gh_sync;
using Octokit;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

public class AdoTests
{
    [Fact]
    public async Task GivenNullIssueThrowException()
    {
        Issue? testIssue = null;
        Func<Task> result = async () => await Ado.PullWorkItemFromIssue(testIssue);
        await Assert.ThrowsAsync<ArgumentNullException>(result);
    }
}