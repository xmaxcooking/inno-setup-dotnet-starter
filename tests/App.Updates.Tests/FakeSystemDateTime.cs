using App.Abstractions.Time;

namespace App.Updates.Tests;

internal sealed class FakeSystemDateTime : ISystemDateTime
{
    public FakeSystemDateTime(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}