namespace App.Abstractions.Processes;

internal interface IProcessLauncher
{
    void Start(string path);
}