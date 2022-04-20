namespace gh_sync.Tests;

using Xunit;
using gh_sync;
using System;
using Octokit;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.Extensions.DependencyInjection;

public record class AdoTests(MockStartup Startup) : IClassFixture<MockStartup>
{
    private IAdo MockAdo => Startup.Services.GetRequiredService<IAdo>();
    Ado Ado = new Ado();

    [Fact]
    public async Task Given_null_id_throw_exception()
    {
        Issue testIssue = new Issue();
        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = null;

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.EnumerateComments(testWorkItem).ToListAsync()
        );

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateCommentsFromIssue(testWorkItem, testIssue).ToListAsync()
        );

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, testIssue)
        );
    }

    [Fact]
    public async Task Given_null_issue_throw_exception()
    {
        Issue? nullIssue = null;
        var testWorkItem = new WorkItem()
        {
            Id = 0
        };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.UpdateCommentsFromIssue(testWorkItem, nullIssue).ToListAsync()
        );

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
    public async Task Given_null_repository_throw_exception()
    {
        Issue testIssue = new Issue();
        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = 0;

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.PullWorkItemFromIssue(testIssue)
        );

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, testIssue)
        );
    }
}

