using App.Updates.Feed;
using System.Reflection;

namespace App.Updates.Settings;

internal sealed class AssemblyMetadataDefaultUpdateChannel : IDefaultUpdateChannel
{
    private const string IsPrereleaseBuildMetadataKey = "IsPrereleaseBuild";

    public UpdateChannel Value
    {
        get
        {
            var metadata = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>();
            var isPrereleaseBuild = metadata?.FirstOrDefault(m => m.Key == IsPrereleaseBuildMetadataKey)?.Value;
            return string.Equals(isPrereleaseBuild, "true", StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Beta : UpdateChannel.Stable;
        }
    }
}