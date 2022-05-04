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
using System.Collections.Generic;
using Spectre.Console;
using System.IO;

public record class SynchronizerTests(MockStartup Startup) : IClassFixture<MockStartup>
{
    internal const string mockUrl = "https://mock.visualstudio.com";
    internal const string foundItemMsg = "Found existing work item:";
    internal const string updateIssNoAllowExistingMsg = "Updating existing issue, since --allow-existing was not set.";
    internal const string noUpdateDryRunMsg = "Not updating new work item in ADO, as --dry-run was set.";
    internal const string notCreatingAsDryRunMsg = "Not creating new work item in ADO, as --dry-run was set.";

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
    public async Task PullIssueDryRunWorksWhenWorkItemExists()
    {
        var oldWriter = AnsiConsole.Console.Profile.Out;
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullIssueDryRunWorksWhenWorkItemExists"),
                dryRun: true
            );
            Assert.Equal($"{foundItemMsg} {mockUrl}\r\n{updateIssNoAllowExistingMsg}\r\n{noUpdateDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = oldWriter;
        }
    }

    [Fact]
    public async Task PullIssueDryRunWorksWhenItemDoesNotExist()
    {
        var oldWriter = AnsiConsole.Console.Profile.Out;
        var writer = new StringWriter();
        AnsiConsole.Console.Profile.Out = new AnsiConsoleOutput(writer);
        try
        {
            await Synchronizer.PullGitHubIssue(
                newIssue("PullIssueDryRunWorksWhenItemDoesNotExist"),
                dryRun: true
            );
            Assert.Equal($"{notCreatingAsDryRunMsg}\r\n", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = oldWriter;
        }
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
