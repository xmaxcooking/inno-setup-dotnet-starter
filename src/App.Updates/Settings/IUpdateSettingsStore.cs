using App.Updates.Feed;

namespace App.Updates.Settings;

public interface IUpdateSettingsStore
{
    UpdateChannel GetChannel();
    void SetChannel(UpdateChannel channel);
}