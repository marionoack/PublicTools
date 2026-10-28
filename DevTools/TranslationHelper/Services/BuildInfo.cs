using System.Reflection;

namespace TranslationHelper.Services;

public static class BuildInfo
{
    // Wird in der .csproj als AssemblyMetadata("BuildDate") beim Build eingebettet.
    public static string BuildDate { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value ?? "unbekannt";
}
