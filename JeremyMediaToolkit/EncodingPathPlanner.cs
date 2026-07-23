using System.IO;

namespace JeremyMediaToolkit;

internal static class EncodingPathPlanner
{
    public static string ResolutionName(OutputResolution resolution) => resolution switch
    {
        OutputResolution.FullHd => "1080p",
        OutputResolution.UltraHd => "4K",
        OutputResolution.Source => "Source",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution))
    };

    public static string OutputRoot(string inputFolder, OutputResolution resolution, RecoveryStrategy recovery)
    {
        var suffix = recovery switch
        {
            RecoveryStrategy.Normal => "",
            RecoveryStrategy.Salvage => "-Salvage",
            RecoveryStrategy.VideoOnly => "-VideoOnly",
            _ => throw new ArgumentOutOfRangeException(nameof(recovery))
        };
        return Path.Combine(inputFolder, $"Toolkit-{ResolutionName(resolution)}-LUT{suffix}");
    }

    public static EncodingJob CreateJob(string inputFolder, string outputRoot, string input, OutputResolution resolution)
    {
        var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(inputFolder, input)) ?? "";
        var output = Path.Combine(outputRoot, relativeDirectory,
            Path.GetFileNameWithoutExtension(input) + $"_{ResolutionName(resolution)}.mp4");
        return new EncodingJob(input, output);
    }
}
