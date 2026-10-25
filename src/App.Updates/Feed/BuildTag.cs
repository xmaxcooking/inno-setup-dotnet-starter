namespace App.Updates.Feed;

public static class BuildTag
{
    public static string Format(long runNumber) => $"build-{runNumber}";

    public static bool TryParse(string tag, out long runNumber)
    {
        runNumber = 0;
        var plus = tag.IndexOf('+');
        var core = plus >= 0 ? tag.Substring(0, plus) : tag;
        var dash = core.LastIndexOf('-');
        return dash >= 0 && long.TryParse(core.Substring(dash + 1), out runNumber);
    }

    public static bool IsNewer(string current, string latest) =>
        TryParse(latest, out var latestN) && TryParse(current, out var currentN) && latestN > currentN;
}