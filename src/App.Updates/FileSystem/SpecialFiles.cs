namespace App.Updates.FileSystem;

internal static class SpecialFiles
{
    public static string LastUpdateCheck => Path.Combine(ApplicationDataDirectory.Path, "last-update-check.txt");
    public static string Settings => Path.Combine(ApplicationDataDirectory.Path, "settings.json");
    public static string ClientId => Path.Combine(ApplicationDataDirectory.Path, "client-id.txt");
}