using App.Abstractions.FileSystem;
using App.Updates.Feed;
using App.Updates.FileSystem;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Updates.Settings;

internal sealed class UpdateSettings : IUpdateSettingsStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IFileSystem _fileSystem;
    private readonly IDefaultUpdateChannel _defaultUpdateChannel;
    private readonly ILogger<UpdateSettings> _logger;

    public UpdateSettings(IFileSystem fileSystem, IDefaultUpdateChannel defaultUpdateChannel, ILogger<UpdateSettings> logger)
    {
        _fileSystem = fileSystem;
        _defaultUpdateChannel = defaultUpdateChannel;
        _logger = logger;
    }

    public UpdateChannel GetChannel()
    {
        try
        {
            if (!_fileSystem.FileExists(SpecialFiles.Settings))
            {
                return _defaultUpdateChannel.Value;
            }

            var settings = JsonSerializer.Deserialize<StoredSettings>(_fileSystem.ReadAllText(SpecialFiles.Settings), _jsonOptions);
            return settings?.Channel ?? _defaultUpdateChannel.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read update-channel setting; using the default channel.");
            return _defaultUpdateChannel.Value;
        }
    }

    public void SetChannel(UpdateChannel channel)
    {
        try
        {
            _fileSystem.CreateDirectory(ApplicationDataDirectory.Path);
            _fileSystem.WriteAllText(SpecialFiles.Settings, JsonSerializer.Serialize(new StoredSettings(channel), _jsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist update-channel setting.");
        }

        try
        {
            _fileSystem.DeleteFile(SpecialFiles.LastUpdateCheck);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reset the update-check cooldown.");
        }
    }

    private sealed class StoredSettings
    {
        public UpdateChannel Channel { get; }

        public StoredSettings(UpdateChannel channel)
        {
            Channel = channel;
        }
    }
}