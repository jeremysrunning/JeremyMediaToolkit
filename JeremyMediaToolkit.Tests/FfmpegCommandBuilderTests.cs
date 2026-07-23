using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class FfmpegCommandBuilderTests
{
    [Fact]
    public void Encode_NormalModeCopiesOptionalAudioAndEscapesLutPath()
    {
        var args = FfmpegCommandBuilder.Encode("input.mov", "output.mp4", @"C:\LUT's\Film.cube", RecoveryStrategy.Normal, OutputResolution.FullHd);

        AssertContainsSequence(args, "-map", "0:a?");
        AssertContainsSequence(args, "-c:a", "copy");
        AssertContainsSequence(args, "-vf", "lut3d=file='C\\:/LUT\\'s/Film.cube',scale=-2:1080");
        Assert.Equal("output.mp4", args[^1]);
    }

    [Fact]
    public void Encode_SalvageModeUsesRecoveryFlagsAndAacResampling()
    {
        var args = FfmpegCommandBuilder.Encode("in", "out", "lut", RecoveryStrategy.Salvage, OutputResolution.Source);

        AssertContainsSequence(args, "-fflags", "+discardcorrupt+genpts", "-err_detect", "ignore_err");
        AssertContainsSequence(args, "-map", "0:a:0?");
        AssertContainsSequence(args, "-c:a", "aac", "-b:a", "192k", "-af", "aresample=async=1:first_pts=0");
        AssertContainsSequence(args, "-vf", "lut3d=file='lut'");
    }

    [Fact]
    public void Encode_VideoOnlyModeOmitsAudioMappingAndDisablesAudio()
    {
        var args = FfmpegCommandBuilder.Encode("in", "out", "lut", RecoveryStrategy.VideoOnly, OutputResolution.UltraHd);

        Assert.DoesNotContain("0:a?", args);
        Assert.DoesNotContain("0:a:0?", args);
        Assert.Contains("-an", args);
        Assert.Contains("lut3d=file='lut',scale=3840:2160:force_original_aspect_ratio=decrease,pad=3840:2160:(ow-iw)/2:(oh-ih)/2", args);
    }

    [Fact]
    public void Encode_RejectsUnknownResolution()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FfmpegCommandBuilder.Encode("in", "out", "lut", RecoveryStrategy.Normal, (OutputResolution)99));
    }

    [Fact]
    public void Encode_DetailedOutputRequestsVerboseFfmpegMessages()
    {
        var args = FfmpegCommandBuilder.Encode("in", "out", "lut", RecoveryStrategy.Normal, OutputResolution.Source, detailedOutput: true);

        AssertContainsSequence(args, "-loglevel", "verbose");
    }

    [Fact]
    public void Encode_NormalOutputUsesStandardFfmpegMessages()
    {
        var args = FfmpegCommandBuilder.Encode("in", "out", "lut", RecoveryStrategy.Normal, OutputResolution.Source);

        AssertContainsSequence(args, "-loglevel", "info");
    }

    [Fact]
    public void ProbeAndInspectArgumentsTargetProvidedFile()
    {
        Assert.Equal(["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", "clip.mov"], FfmpegCommandBuilder.ProbeDuration("clip.mov"));
        Assert.Equal(["-hide_banner", "-show_format", "-show_streams", "clip.mov"], FfmpegCommandBuilder.Inspect("clip.mov"));
    }

    [Fact]
    public void VerifyAndRewrapArgumentsPreserveExpectedOperations()
    {
        Assert.Equal(["-v", "warning", "-i", "in", "-map", "0:v:0", "-f", "null", "NUL"], FfmpegCommandBuilder.Verify("in"));
        Assert.Equal(["-hide_banner", "-y", "-i", "in", "-map", "0", "-c", "copy", "-movflags", "+faststart", "out"], FfmpegCommandBuilder.Rewrap("in", "out"));
    }

    [Fact]
    public void ProxyAndContactSheetArgumentsPreserveOutputsAndFilters()
    {
        var proxy = FfmpegCommandBuilder.Proxy("in", "proxy.mp4");
        AssertContainsSequence(proxy, "-vf", "scale=-2:1080");
        Assert.Equal("proxy.mp4", proxy[^1]);
        var sheet = FfmpegCommandBuilder.ContactSheet("in", "sheet.jpg");
        AssertContainsSequence(sheet, "-vf", "fps=1/10,scale=480:-1,tile=4x4:padding=8:margin=8");
        Assert.Equal("sheet.jpg", sheet[^1]);
    }

    private static void AssertContainsSequence(IReadOnlyList<string> actual, params string[] expected)
    {
        for (var index = 0; index <= actual.Count - expected.Length; index++)
            if (actual.Skip(index).Take(expected.Length).SequenceEqual(expected)) return;
        Assert.Fail($"Expected sequence was not found: {string.Join(" ", expected)}");
    }
}
