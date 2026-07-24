using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class EncodingPathPlannerTests
{
    [Theory]
    [InlineData((int)OutputResolution.Sd480, "480p")]
    [InlineData((int)OutputResolution.Hd720, "720p")]
    [InlineData((int)OutputResolution.FullHd, "1080p")]
    [InlineData((int)OutputResolution.Qhd1440, "1440p")]
    [InlineData((int)OutputResolution.UltraHd, "4K")]
    [InlineData((int)OutputResolution.Source, "Source")]
    public void ResolutionName_MapsEveryResolution(int resolution, string expected)
    {
        Assert.Equal(expected, EncodingPathPlanner.ResolutionName((OutputResolution)resolution));
    }

    [Theory]
    [InlineData((int)RecoveryStrategy.Normal, "Lightflow-1080p-LUT")]
    [InlineData((int)RecoveryStrategy.Salvage, "Lightflow-1080p-LUT-Salvage")]
    [InlineData((int)RecoveryStrategy.VideoOnly, "Lightflow-1080p-LUT-VideoOnly")]
    public void OutputRoot_MapsEveryRecoveryMode(int recovery, string expectedFolder)
    {
        Assert.EndsWith(expectedFolder, EncodingPathPlanner.OutputRoot(@"C:\videos", OutputResolution.FullHd, (RecoveryStrategy)recovery));
    }

    [Fact]
    public void OutputRoot_SeparatesNonDefaultCodecAndContainer()
    {
        var options = EncodingPresetCatalog.Get(EncodingPreset.EfficientHevc) with { Container = OutputContainer.Mkv };

        var output = EncodingPathPlanner.OutputRoot("input", OutputResolution.UltraHd, RecoveryStrategy.Normal, options);

        Assert.EndsWith("Lightflow-4K-LUT-HEVC-MKV", output);
    }
    [Fact]
    public void CreateJob_PreservesRelativeFoldersAndUsesMp4Output()
    {
        var inputRoot = Path.Combine("C:", "videos");
        var outputRoot = Path.Combine(inputRoot, "Lightflow-4K-LUT");
        var input = Path.Combine(inputRoot, "day-one", "clip.mov");

        var job = EncodingPathPlanner.CreateJob(inputRoot, outputRoot, input, OutputResolution.UltraHd);

        Assert.Equal(input, job.InputPath);
        Assert.Equal(Path.Combine(outputRoot, "day-one", "clip_4K.mp4"), job.OutputPath);
    }

    [Theory]
    [InlineData(OutputContainer.Mp4, ".mp4")]
    [InlineData(OutputContainer.Mkv, ".mkv")]
    [InlineData(OutputContainer.Mov, ".mov")]
    internal void CreateJob_UsesSelectedContainer(OutputContainer container, string extension)
    {
        var job = EncodingPathPlanner.CreateJob("input", "output", Path.Combine("input", "clip.mov"),
            OutputResolution.Source, container);

        Assert.EndsWith(extension, job.OutputPath);
    }
    [Fact]
    public void CreateJob_UsesCustomOrEmptyFilenameSuffix()
    {
        var custom = EncodingPathPlanner.CreateJob("input", "output", Path.Combine("input", "clip.mov"),
            OutputResolution.FullHd, filenameSuffix: "_Social");
        var noSuffix = EncodingPathPlanner.CreateJob("input", "output", Path.Combine("input", "clip.mov"),
            OutputResolution.FullHd, filenameSuffix: "");

        Assert.EndsWith("clip_Social.mp4", custom.OutputPath);
        Assert.EndsWith("clip.mp4", noSuffix.OutputPath);
    }
    [Fact]
    public void CreateJob_FlattensNestedInputWhenFolderStructureIsNotPreserved()
    {
        var inputRoot = Path.Combine("C:", "videos");
        var outputRoot = Path.Combine("D:", "exports");
        var input = Path.Combine(inputRoot, "day-one", "clip.mov");

        var job = EncodingPathPlanner.CreateJob(inputRoot, outputRoot, input,
            OutputResolution.FullHd, preserveFolderStructure: false);

        Assert.Equal(Path.Combine(outputRoot, "clip_1080p.mp4"), job.OutputPath);
    }

    [Fact]
    public void HasOutputCollisions_DetectsDuplicateFlattenedNames()
    {
        var first = new EncodingJob("one", Path.Combine("output", "clip.mp4"));
        var second = new EncodingJob("two", Path.Combine("output", "CLIP.mp4"));

        Assert.True(EncodingPathPlanner.HasOutputCollisions([first, second]));
        Assert.False(EncodingPathPlanner.HasOutputCollisions([first]));
    }
    [Fact]
    public void ResolutionName_RejectsUnknownValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EncodingPathPlanner.ResolutionName((OutputResolution)99));
    }
}
