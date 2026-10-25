using App.Updates.Feed;

namespace App.Updates.Settings;

public interface IDefaultUpdateChannel
{
    UpdateChannel Value { get; }
}