using App.Infrastructure;
using App.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreApi(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddInfrastructure();
        services.AddUpdates();

        services.AddSingleton<ICustomerDirectory, CustomerDirectory>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IUpdateChannelSettings, UpdateChannelSettings>();

        return services;
    }
}