using App.Updates.Feed;

namespace App.Updates.Tests;

internal sealed class FakeUpdateFeed : IUpdateFeed
{
    private readonly UpdateCheckResult _result;
    private readonly Exception? _failure;

    public FakeUpdateFeed(UpdateCheckResult result)
    {
        _result = result;
    }

    private FakeUpdateFeed(Exception failure)
    {
        _result = UpdateCheckResult.None;
        _failure = failure;
    }

    public static FakeUpdateFeed ThatFails(Exception failure) => new FakeUpdateFeed(failure);

    public bool WasCalled { get; private set; }
    public string? ReceivedClientId { get; private set; }

    public Task<UpdateCheckResult> TryGetLatestIfNewerAsync(string currentVersion, string clientId, CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        ReceivedClientId = clientId;

        if (_failure is not null)
        {
            throw _failure;
        }

        return Task.FromResult(_result);
    }
}