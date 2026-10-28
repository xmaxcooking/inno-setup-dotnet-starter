using App.Updates.Feed;

namespace App.Updates.Settings;

internal interface IDefaultUpdateChannel
{
    UpdateChannel Value { get; }
}