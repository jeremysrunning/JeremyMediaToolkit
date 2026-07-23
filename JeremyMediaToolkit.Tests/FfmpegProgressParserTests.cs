using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class FfmpegProgressParserTests
{
    [Theory]
    [InlineData("out_time_us=5000000", 10, 50)]
    [InlineData("out_time_us=20000000", 10, 100)]
    [InlineData("out_time_us=-1000", 10, 0)]
    public void TryParsePercent_ParsesAndClampsProgress(string line, double duration, double expected)
    {
        Assert.True(FfmpegProgressParser.TryParsePercent(line, duration, out var percent));
        Assert.Equal(expected, percent);
    }

    [Theory]
    [InlineData("progress=continue", 10)]
    [InlineData("out_time_us=invalid", 10)]
    [InlineData("out_time_us=1000", 0)]
    [InlineData("out_time_us=1000", -1)]
    public void TryParsePercent_RejectsInvalidInput(string line, double duration)
    {
        Assert.False(FfmpegProgressParser.TryParsePercent(line, duration, out var percent));
        Assert.Equal(0, percent);
    }
}
