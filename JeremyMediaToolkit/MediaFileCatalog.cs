using System.IO;

namespace JeremyMediaToolkit;

internal static class MediaFileCatalog
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".mxf"
    };

    public static IReadOnlyList<string> Discover(string folder, bool recursive)
    {
        if (!Directory.Exists(folder)) return [];
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(folder, "*", option)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !IsToolkitOutput(folder, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsToolkitOutput(string root, string path) => Path.GetRelativePath(root, path)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .SkipLast(1)
        .Any(part => part.StartsWith("Toolkit-", StringComparison.OrdinalIgnoreCase));
}
