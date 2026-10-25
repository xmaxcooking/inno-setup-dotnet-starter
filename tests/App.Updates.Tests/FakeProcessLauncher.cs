using App.Abstractions.Processes;

namespace App.Updates.Tests;

internal sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<string> StartedPaths { get; } = new();

    public void Start(string path) => StartedPaths.Add(path);
}