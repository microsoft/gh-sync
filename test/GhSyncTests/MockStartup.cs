// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Octokit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Threading.Tasks;
using Moq;

using Xunit;
using gh_sync;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Spectre.Console;
using System.IO;

namespace gh_sync.Tests;

internal static class MockServiceExtensions
{
    public static IServiceCollection AddMock<T>(this IServiceCollection services, Action<Mock<T>>? configure = null)
    where T: class
    {
        var mock = new Mock<T>();
        configure?.Invoke(mock);
        services.AddSingleton<T>(mock.Object);
        return services;
    }
}

public class MockStartup
{
    private Issue testIssue = new Issue();
    private readonly Lazy<IServiceProvider> services;
    public IServiceProvider Services => services.Value;

    public MockStartup()
    {
        this.services = new Lazy<IServiceProvider>(() =>
        {
            var serviceCollection = new ServiceCollection();
            this.ConfigureServices(serviceCollection);
            return serviceCollection.BuildServiceProvider();
        });
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMock<IAdo>(mock =>
        {
            mock
                .Setup(arg => arg.UpdateFromIssue(It.IsAny<WorkItem>(), It.IsAny<Issue?>()))
                .Returns(Task.FromResult(new WorkItem()));
            mock
                .Setup(arg => arg.GetAdoWorkItem(It.Is<Issue>(issue => issue.Title == "PullIssueDryRunWorksWhenWorkItemExists")))
                .Returns(
                    Task.FromResult<WorkItem?>(new()
                    {
                        Url = "https://mock.visualstudio.com",
                        Id = 12345,
                        Links = new()
                    })
                );
            mock
                .Setup(arg => arg.GetAdoWorkItem(It.Is<Issue>(issue => issue.Title == "PullIssueDryRunWorksWhenItemDoesNotExist")))
                .Returns(
                    Task.FromResult<WorkItem?>(null)
                );
            mock
                .Setup(arg => arg.GetAdoWorkItem(It.Is<Issue>(issue => issue.Title == "PullIssueDryRunAllowExisting")))
                .Returns(
                    Task.FromResult<WorkItem?>(new()
                    {
                        Url = "https://mock.visualstudio.com",
                        Id = 12345,
                        Links = new()
                    })
                );
        });
        services.AddMock<IGitHub>(
        );
        services.AddMock<ISynchronizer>(
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
