// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Octokit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Threading.Tasks;
using Moq;

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
                .Setup(arg => arg.GetAdoWorkItem(It.IsAny<Issue>()))
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
}
