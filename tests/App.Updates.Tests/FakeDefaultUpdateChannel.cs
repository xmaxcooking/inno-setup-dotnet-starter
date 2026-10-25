using App.Updates.Feed;
using App.Updates.Settings;

namespace App.Updates.Tests;

internal sealed class FakeDefaultUpdateChannel : IDefaultUpdateChannel
{
    public UpdateChannel Value { get; set; } = UpdateChannel.Stable;
}