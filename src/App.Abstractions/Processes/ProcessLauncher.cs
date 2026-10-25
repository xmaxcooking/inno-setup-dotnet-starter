using System.Diagnostics;

namespace App.Abstractions.Processes;

public sealed class ProcessLauncher : IProcessLauncher
{
    public void Start(string path)
    {
        var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
        Process.Start(startInfo);
    }
}