namespace App.Abstractions.Processes;

public interface IProcessLauncher
{
    void Start(string path);
}