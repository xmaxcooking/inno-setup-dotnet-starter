namespace App.Updates.Tests;

internal sealed class FakeClientIdentity : IClientIdentity
{
    private readonly string _id;

    public FakeClientIdentity(string id)
    {
        _id = id;
    }

    public string GetOrCreate() => _id;
}