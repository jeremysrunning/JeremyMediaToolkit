using System.IO;

namespace LightflowStudio;

internal static class EncodingPathPlanner
{
    public static string ResolutionName(OutputResolution resolution) => resolution switch
    {
        OutputResolution.FullHd => "1080p",
        OutputResolution.UltraHd => "4K",
        OutputResolution.Source => "Source",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution))
    };

    public static string OutputRoot(string inputFolder, OutputResolution resolution, RecoveryStrategy recovery, EncodingOptions? encoding = null)
    {
        var suffix = recovery switch
        {
            RecoveryStrategy.Normal => "",
            RecoveryStrategy.Salvage => "-Salvage",
            RecoveryStrategy.VideoOnly => "-VideoOnly",
            _ => throw new ArgumentOutOfRangeException(nameof(recovery))
        };
        var options = EncodingOptions.Normalize(encoding);
        var codecSuffix = options.Codec == VideoCodec.Hevc ? "-HEVC" : "";
        var containerSuffix = options.Container switch
        {
            OutputContainer.Mp4 => "",
            OutputContainer.Mkv => "-MKV",
            OutputContainer.Mov => "-MOV",
            _ => throw new ArgumentOutOfRangeException(nameof(options.Container))
        };
        return Path.Combine(inputFolder, $"Lightflow-{ResolutionName(resolution)}-LUT{suffix}{codecSuffix}{containerSuffix}");
    }

    public static string ContainerExtension(OutputContainer container) => container switch
    {
        OutputContainer.Mp4 => ".mp4",
        OutputContainer.Mkv => ".mkv",
        OutputContainer.Mov => ".mov",
        _ => throw new ArgumentOutOfRangeException(nameof(container))
    };

    public static EncodingJob CreateJob(string inputFolder, string outputRoot, string input, OutputResolution resolution, OutputContainer container = OutputContainer.Mp4, string? filenameSuffix = null)
    {
        var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(inputFolder, input)) ?? "";
        var output = Path.Combine(outputRoot, relativeDirectory,
            Path.GetFileNameWithoutExtension(input) + (filenameSuffix ?? $"_{ResolutionName(resolution)}") + ContainerExtension(container));
        return new EncodingJob(input, output);
    }
}
