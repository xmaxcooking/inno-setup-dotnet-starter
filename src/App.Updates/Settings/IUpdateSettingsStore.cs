using App.Updates.Feed;

namespace App.Updates.Settings;

internal interface IUpdateSettingsStore
{
    UpdateChannel GetChannel();
    void SetChannel(UpdateChannel channel);
}