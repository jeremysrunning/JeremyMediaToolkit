using System.IO;

namespace LightflowStudio;

internal enum OutputDestinationMode
{
    SameFolder,
    Subfolder,
    SpecificFolder
}

internal sealed record OutputDestinationOptions(
    OutputDestinationMode Mode,
    string SubfolderName,
    string SpecificFolder,
    string SameFolderSuffix);

internal static class OutputDestinationPlanner
{
    public static string ResolveRoot(string inputFolder, OutputResolution resolution, OutputDestinationOptions options)
    {
        if (!Directory.Exists(inputFolder)) throw new ArgumentException("Select a valid video folder.", nameof(inputFolder));
        if (!Enum.IsDefined(options.Mode)) throw new ArgumentOutOfRangeException(nameof(options));

        return options.Mode switch
        {
            OutputDestinationMode.SameFolder => inputFolder,
            OutputDestinationMode.Subfolder => Path.Combine(inputFolder, ResolveSubfolderName(resolution, options.SubfolderName)),
            OutputDestinationMode.SpecificFolder when !string.IsNullOrWhiteSpace(options.SpecificFolder) =>
                ResolveSpecificFolder(inputFolder, options.SpecificFolder),
            OutputDestinationMode.SpecificFolder =>
                throw new ArgumentException("Choose a specific output folder.", nameof(options)),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    }


    private static string ResolveSpecificFolder(string inputFolder, string folder)
    {
        var resolved = Path.GetFullPath(folder.Trim());
        if (string.Equals(resolved.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(inputFolder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose Same folder as source when the output destination is the source folder.", nameof(folder));
        return resolved;
    }
    public static string ResolveFilenameSuffix(OutputResolution resolution, OutputDestinationOptions options)
    {
        if (options.Mode != OutputDestinationMode.SameFolder) return "";
        var suffix = string.IsNullOrWhiteSpace(options.SameFolderSuffix)
            ? $"_{EncodingPathPlanner.ResolutionName(resolution)}"
            : options.SameFolderSuffix.Trim();
        if (suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || suffix.Contains(Path.DirectorySeparatorChar)
            || suffix.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("The filename suffix contains characters that cannot be used in a filename.", nameof(options));
        return suffix;
    }
    public static string ResolveSubfolderName(OutputResolution resolution, string name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? EncodingPathPlanner.ResolutionName(resolution) : name.Trim();
        if (Path.IsPathRooted(value)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar)
            || value is "." or "..")
            throw new ArgumentException("The output subfolder must be a single valid folder name.", nameof(name));
        return value;
    }
}
