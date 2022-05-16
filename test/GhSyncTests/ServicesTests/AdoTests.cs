// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.GhSync.Tests;

using Xunit;
using System;
using Octokit;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Common;

public record class AdoTests(MockStartup Startup) : IClassFixture<MockStartup>
{
    private Lazy<IAdo> adoLazy = new Lazy<IAdo>(
        () => ActivatorUtilities.CreateInstance<Ado>(Startup.Services)
    );
    private IAdo Ado => adoLazy.Value;

    [Fact]
    public void CanCreateAdoFromMocks()
    {
        Assert.NotNull(Ado);
    }

    [Fact]
    public async Task GetAdoConnectionReturnsConnectionIfNotNull()
    {
        var testUri = new Uri("http://some-uri");
        var testCreds = new VssBasicCredential("", "some-token");
        VssConnection? testConnection = new VssConnection(testUri, testCreds);

        Assert.NotNull(await Ado.GetAdoConnection("bad-token", testConnection));
    }

    [Fact]
    public async Task GetAdoConnectionThrowsExceptionGivenBadToken()
    {
        VssConnection? nullConnection = null;

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.GetAdoConnection("bad-token", nullConnection)
        );
    }

    [Fact]
    public async Task GetAdoConnectionThrowsExceptionGivenUnauthorizedToken()
    {
        VssConnection? nullConnection = null;

        await Assert.ThrowsAsync<AuthorizationException>(
            async () => await Ado.GetAdoConnection("unauthorized-token", nullConnection)
        );
    }

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

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Ado.UpdateFromIssue(testWorkItem, testIssue)
        );
    }
}

