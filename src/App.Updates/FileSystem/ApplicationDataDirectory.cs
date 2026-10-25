namespace App.Updates.FileSystem;

internal static class ApplicationDataDirectory
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InnoSetupDotnetStarter");
}