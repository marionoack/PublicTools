namespace SqlTransfer.Infrastructure;

public static class BuildInfo
{
    public static string BuildDate => GeneratedBuildDate.Value;

    public static string Title => $"SqlTransfer – Build {BuildDate}";
}
