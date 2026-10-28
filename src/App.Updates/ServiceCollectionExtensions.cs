using App.Abstractions.FileSystem;
using App.Abstractions.Processes;
using App.Abstractions.Time;
using App.Updates.Feed;
using App.Updates.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace App.Updates;

public static class ServiceCollectionExtensions
{
    private const string GitHubOwnerMetadataKey = "GitHubOwner";
    private const string GitHubRepoMetadataKey = "GitHubRepo";

    public static IServiceCollection AddUpdates(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystemIO>();
        services.AddSingleton<IClientIdentity, ClientIdentity>();
        services.AddSingleton<IDefaultUpdateChannel, AssemblyMetadataDefaultUpdateChannel>();
        services.AddSingleton<IUpdateSettingsStore, UpdateSettings>();
        services.AddSingleton<ISystemDateTime, SystemDateTime>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IUpdateFeed>(createUpdateFeed);
        services.AddSingleton<UpdateCheck>();
        return services;
    }

    private static IUpdateFeed createUpdateFeed(IServiceProvider sp)
    {
        var metadata = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
        var owner = metadata?.FirstOrDefault(m => m.Key == GitHubOwnerMetadataKey)?.Value ?? string.Empty;
        var repo = metadata?.FirstOrDefault(m => m.Key == GitHubRepoMetadataKey)?.Value ?? string.Empty;
        var channel = sp.GetRequiredService<IUpdateSettingsStore>().GetChannel();

        return new GitHubReleaseUpdateFeed(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GitHubReleaseUpdateFeed)),
            owner, repo, channel, sp.GetRequiredService<ILogger<GitHubReleaseUpdateFeed>>());
    }
}