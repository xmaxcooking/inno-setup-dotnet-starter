namespace App.Abstractions.Time;

internal sealed class SystemDateTime : ISystemDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}