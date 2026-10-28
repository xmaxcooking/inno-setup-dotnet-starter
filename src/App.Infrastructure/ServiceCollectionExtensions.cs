using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppDbContext, FakeDbContext>();
        return services;
    }
}