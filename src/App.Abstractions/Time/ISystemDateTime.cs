namespace App.Abstractions.Time;

public interface ISystemDateTime
{
    DateTimeOffset UtcNow { get; }
}