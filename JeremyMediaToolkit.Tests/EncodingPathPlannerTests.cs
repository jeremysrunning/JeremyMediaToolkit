using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class EncodingPathPlannerTests
{
    [Theory]
    [InlineData((int)OutputResolution.FullHd, "1080p")]
    [InlineData((int)OutputResolution.UltraHd, "4K")]
    [InlineData((int)OutputResolution.Source, "Source")]
    public void ResolutionName_MapsEveryResolution(int resolution, string expected)
    {
        Assert.Equal(expected, EncodingPathPlanner.ResolutionName((OutputResolution)resolution));
    }

    [Theory]
    [InlineData((int)RecoveryStrategy.Normal, "Toolkit-1080p-LUT")]
    [InlineData((int)RecoveryStrategy.Salvage, "Toolkit-1080p-LUT-Salvage")]
    [InlineData((int)RecoveryStrategy.VideoOnly, "Toolkit-1080p-LUT-VideoOnly")]
    public void OutputRoot_MapsEveryRecoveryMode(int recovery, string expectedFolder)
    {
        Assert.EndsWith(expectedFolder, EncodingPathPlanner.OutputRoot(@"C:\videos", OutputResolution.FullHd, (RecoveryStrategy)recovery));
    }

    [Fact]
    public void CreateJob_PreservesRelativeFoldersAndUsesMp4Output()
    {
        var inputRoot = Path.Combine("C:", "videos");
        var outputRoot = Path.Combine(inputRoot, "Toolkit-4K-LUT");
        var input = Path.Combine(inputRoot, "day-one", "clip.mov");

        var job = EncodingPathPlanner.CreateJob(inputRoot, outputRoot, input, OutputResolution.UltraHd);

        Assert.Equal(input, job.InputPath);
        Assert.Equal(Path.Combine(outputRoot, "day-one", "clip_4K.mp4"), job.OutputPath);
    }

    [Fact]
    public void ResolutionName_RejectsUnknownValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EncodingPathPlanner.ResolutionName((OutputResolution)99));
    }
}
