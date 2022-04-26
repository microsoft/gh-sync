// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Octokit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Threading.Tasks;
using Moq;

namespace gh_sync.Tests;

public class MockStartup
{
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
        services.AddSingleton<IAdo>(
            new Moq.Mock<IAdo>().Setup(arg => arg.UpdateFromIssue(It.IsAny<WorkItem>(), It.IsAny<Issue?>())).Returns(Task.FromResult(new WorkItem()))
            .Object
        );
        services.AddSingleton<IGitHub>(
            new Moq.Mock<IGitHub>()
            .Object
        );
        services.AddSingleton<ISynchronizer>(
            new Moq.Mock<ISynchronizer>()
            .Object
        );
    }
}
