using System;
using Microsoft.Extensions.DependencyInjection;

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
            new Moq.Mock<IAdo>()
            // TODO: .Setup call here.
            .Object
        );
    }
}
