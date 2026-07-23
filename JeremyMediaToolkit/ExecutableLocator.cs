using System.IO;

namespace JeremyMediaToolkit;

internal static class ExecutableLocator
{
    public static string? Find(string name, string bundled, string? pathEnvironment = null)
    {
        if (File.Exists(bundled)) return bundled;
        var path = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => Path.Combine(entry.Trim('"'), name))
            .FirstOrDefault(File.Exists);
    }
}
