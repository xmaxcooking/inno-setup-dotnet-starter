using App.Updates.Feed;
using App.Updates.Settings;

namespace App.Core.Api;

internal sealed class UpdateChannelSettings : IUpdateChannelSettings
{
    private readonly IUpdateSettingsStore _settingsStore;

    public UpdateChannelSettings(IUpdateSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public UpdateChannel GetChannel() => _settingsStore.GetChannel();

    public void SetChannel(UpdateChannel channel) => _settingsStore.SetChannel(channel);
}