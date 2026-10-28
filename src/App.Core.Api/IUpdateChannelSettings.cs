using App.Updates.Feed;

namespace App.Core.Api;

public interface IUpdateChannelSettings
{
    UpdateChannel GetChannel();

    void SetChannel(UpdateChannel channel);
}