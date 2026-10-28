using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppDbContext, FakeDbContext>();
        return services;
    }
}