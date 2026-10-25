namespace App.Abstractions.Time;

public sealed class SystemDateTime : ISystemDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}