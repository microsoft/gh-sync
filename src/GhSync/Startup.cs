// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.GhSync;

public class Startup
{
    public virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOptions, Options>();
        services.AddSingleton<IAdo, Ado>();
        services.AddSingleton<IGitHub, GitHub>();
        services.AddSingleton<ISynchronizer, Synchronizer>();
    }
}
