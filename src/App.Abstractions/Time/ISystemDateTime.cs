namespace App.Abstractions.Time;

internal interface ISystemDateTime
{
    DateTimeOffset UtcNow { get; }
}