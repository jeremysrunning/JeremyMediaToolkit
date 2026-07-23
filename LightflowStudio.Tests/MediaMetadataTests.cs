using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public class MediaMetadataTests
{
    private const string ProbeJson = """
        {
          "streams": [
            { "codec_type": "video", "codec_name": "hevc", "width": 3840, "height": 2160, "avg_frame_rate": "60000/1001" },
            { "codec_type": "audio", "codec_name": "aac" }
          ],
          "format": { "duration": "134.5" }
        }
        """;

    [Fact]
    public void Parser_ReadsVideoAudioAndFormatDetails()
    {
        Assert.True(MediaMetadataParser.TryParse(ProbeJson, 4_294_967_296, out var metadata));
        Assert.Equal(3840, metadata.Width);
        Assert.Equal(2160, metadata.Height);
        Assert.Equal(59.94, metadata.FrameRate, 2);
        Assert.Equal(134.5, metadata.DurationSeconds);
        Assert.Equal("hevc", metadata.VideoCodec);
        Assert.True(metadata.HasAudio);
    }

    [Fact]
    public void Parser_RejectsInvalidJsonAndMissingVideo()
    {
        Assert.False(MediaMetadataParser.TryParse("not json", 0, out _));
        Assert.False(MediaMetadataParser.TryParse("""{"streams":[{"codec_type":"audio"}]}""", 0, out _));
    }

    [Fact]
    public void Presentation_ProducesCompactFriendlyDetails()
    {
        var metadata = new MediaMetadata(3840, 2160, 59.94, 134.5, 4_294_967_296, "hevc", true);
        Assert.Equal("3840×2160 · 59.94 fps · 2:14 · 4 GB", MediaMetadataPresentation.Details(metadata));
        Assert.Equal("HEVC video · Audio present", MediaMetadataPresentation.Tooltip(metadata));
    }

    [Fact]
    public void WarningAnalyzer_FlagsMissingAudioAndOutliers()
    {
        var first = Option("first", new MediaMetadata(3840, 2160, 59.94, 60, 100, "hevc", true));
        var second = Option("second", new MediaMetadata(3840, 2160, 59.94, 60, 100, "hevc", true));
        var outlier = Option("outlier", new MediaMetadata(1920, 1080, 29.97, 60, 100, "h264", false));
        MediaWarningAnalyzer.Apply([first, second, outlier]);
        Assert.False(first.HasWarning);
        Assert.Equal("NO AUDIO", outlier.WarningLabel);
        Assert.Contains("Resolution differs", outlier.WarningTooltip);
        Assert.Contains("Frame rate differs", outlier.WarningTooltip);
    }

    [Fact]
    public void Summary_IncludesSelectedDurationAndSizeAfterAnalysis()
    {
        var first = Option("first", new MediaMetadata(1920, 1080, 30, 60, 1024, "h264", true));
        var second = Option("second", new MediaMetadata(1920, 1080, 30, 90, 1024, "h264", true));
        second.IsSelected = false;
        Assert.Equal("1 of 2 selected · 1 KB · 1:00 total", BatchFileSelection.Summary([first, second]));
    }

    private static BatchFileOption Option(string name, MediaMetadata metadata)
    {
        var option = new BatchFileOption(name, name, metadata.FileSizeBytes);
        option.ApplyMetadata(metadata);
        return option;
    }
}