// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    private IGitHub MockGh => Startup.Services.GetRequiredService<IGitHub>();
    private ISynchronizer MockSync => Startup.Services.GetRequiredService<ISynchronizer>();

    Ado Ado = new Ado();
    GitHub GitHub = new GitHub();

    [Fact]
    // <summary>
    //      Checks that passing <c>null</c> as an issue or work item ID throws an exception.
    // </summary>
    public async Task GivenNullIdThrowException()
    {
        Issue testIssue = new Issue();
        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = null;

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.EnumerateComments(testWorkItem).ToListAsync()
        );

        // await Assert.ThrowsAsync<NullReferenceException>(
        //     async () => await Ado.UpdateCommentsFromIssue(Startup.Services, testWorkItem, testIssue).ToListAsync()
        // );

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, testIssue)
        );
    }

    [Fact]
    // <summary>
    //      Checks that passing <c>null<c> as an issue or work item throws an exception.
    // </summary>
    public async Task GivenNullIssueThrowException()
    {
        Issue? nullIssue = null;
        var testWorkItem = new WorkItem()
        {
            Id = 0
        };

        // await Assert.ThrowsAsync<ArgumentNullException>(
        //     async () => await Ado.UpdateCommentsFromIssue(Startup.Services, testWorkItem, nullIssue).ToListAsync()
        // );

        // await Assert.ThrowsAsync<ArgumentNullException>(
        //     async () => await Ado.PullWorkItemFromIssue(Startup.Services, nullIssue)
        // );

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, nullIssue)
        );

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Ado.GetAdoWorkItem(nullIssue)
        );
    }

    [Fact]
    // <summary>
    //      Checks that passing <c>null</c> as an issue repository throws an exception.
    // </summary>
    public async Task GivenNullRepositoryThrowException()
    {
        Issue testIssue = new Issue();
        WorkItem testWorkItem = new WorkItem();
        testWorkItem.Id = 0;

        // await Assert.ThrowsAsync<NullReferenceException>(
        //     async () => await Ado.PullWorkItemFromIssue(Startup.Services, testIssue)
        // );

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, testIssue)
        );
    }
}

