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
                new Issue(
                    "",
                    "",
                    "",
                    "",
                    123456,
                    ItemState.Open,
                    title: "Mock issue",
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
                ),
                dryRun: true
            );
            Assert.Equal("", writer.ToString());
        }
        finally
        {
            AnsiConsole.Console.Profile.Out = oldWriter;
        }
    }

}
