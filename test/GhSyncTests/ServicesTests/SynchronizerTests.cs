// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.GhSync.Tests;

using Xunit;
using System;
using Octokit;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Collections.Generic;
using Spectre.Console;
using System.IO;
using System.Linq;
using System.Threading;

public record class SynchronizerTests(MockStartup Startup) : IClassFixture<MockStartup>
{
    internal const string mockUrl = "https://mock.visualstudio.com";
    internal const string foundItemMsg = "Found existing work item:";
    internal const string updateIssNoAllowExistingMsg = "Updating existing issue, since --allow-existing was not set.";
    internal const string noUpdateDryRunMsg = "Not updating new work item in ADO, as --dry-run was set.";
    internal const string notCreatingAsDryRunMsg = "Not creating new work item in ADO, as --dry-run was set.";
    internal const string createNewAllowExistingMsg = "Creating new work item, since --allow-existing was set.";

    private Issue testIssue = new Issue();
    private Issue? nullIssue = null;
    private WorkItem testWorkItem = new()
    {
        Url = "https://mock.visualstudio.com",
        Id = 12345,
        Links = new()
    };
    
    private Lazy<ISynchronizer> synchronizerLazy = new Lazy<ISynchronizer>(
        () => ActivatorUtilities.CreateInstance<Synchronizer>(Startup.Services)
    );
    private ISynchronizer Synchronizer => synchronizerLazy.Value;
    
    [Fact]
    public void CanCreateSynchronizerFromMocks()
    {
        Assert.NotNull(Synchronizer);
    }

    [Fact]
    public async Task PullGitHubIssueDryRunWorksWhenWorkItemExists()
    {
        Thread.Sleep(300);
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullGitHubIssueDryRunWorksWhenWorkItemExists"),
                dryRun: true
            );
            Assert.Equal($"{foundItemMsg} {mockUrl}\r\n{updateIssNoAllowExistingMsg}\r\n{noUpdateDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(new StringWriter());
        }
    }

    [Fact]
    public async Task PullGitHubIssueDryRunWorksWhenItemDoesNotExist()
    {
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullGitHubIssueDryRunWorksWhenItemDoesNotExist"),
                dryRun: true
            );
            Assert.Equal($"{notCreatingAsDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(new StringWriter());
        }
    }

    [Fact]
    public async Task PullGitHubIssueDryRunAllowExistingWhenWorkItemExists()
    {
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullGitHubIssueDryRunAllowExistingWhenWorkItemExists"),
                dryRun: true,
                allowExisting: true
            );
            Assert.Equal($"{foundItemMsg} {mockUrl}\r\n{createNewAllowExistingMsg}\r\n{notCreatingAsDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(new StringWriter());
        }
    }

    [Fact]
    public async Task PullGitHubIssueDryRunAllowExistingWhenWorkItemDoesNotExist()
    {
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullGitHubIssueDryRunAllowExistingWhenWorkItemDoesNotExist"),
                dryRun: true,
                allowExisting: true
            );
            Assert.Equal($"{notCreatingAsDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(new StringWriter());
        }
    }

    [Fact]
    public async Task UpdateCommentsFromIssueThrowsExceptionGivenNullIssue()
    {   
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Synchronizer.UpdateCommentsFromIssue(testWorkItem, nullIssue).ToListAsync()
        );
    }

    [Fact]
    public async Task UpdateCommentsFromIssueThrowsExceptionGivenNullWorkItem()
    {
        WorkItem workItemNullId = new WorkItem();

        Assert.Null(workItemNullId.Id);

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Synchronizer.UpdateCommentsFromIssue(workItemNullId, newIssue("")).ToListAsync()
        );
    }

    [Fact]
    public async Task PullWorkItemFromIssueThrowsExceptionGivenNullIssue()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Synchronizer.PullWorkItemFromIssue(nullIssue)
        );
    }

    [Fact]
    public async Task PullWorkItemFromIssueThrowsExceptionGivenNullRepository()
    {
        Issue issueNullRepository = new Issue();

        await Assert.ThrowsAsync<NullReferenceException>(
            async () => await Synchronizer.PullWorkItemFromIssue(issueNullRepository)
        );
    }

    private Issue newIssue(string title = "") {
        return new Issue(
            "",
            "",
            "",
            "",
            number: 123456,
            ItemState.Open,
            title: title,
            body: "",
            closedBy: null,
            user: null,
            labels: new List<Label>().AsReadOnly(),
            assignee: null,
            assignees: new List<User>().AsReadOnly(),
            milestone: null,
            comments: 12,
            pullRequest: null,
            closedAt: null,
            createdAt: DateTimeOffset.Now,
            updatedAt: DateTimeOffset.Now,
            id: 1234567,
            nodeId: "",
            locked: false,
            repository: new Repository(),
            reactions: null
        );
    }
}
