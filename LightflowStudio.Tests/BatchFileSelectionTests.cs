using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class BatchFileSelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"LightflowStudio-Batch-{Guid.NewGuid():N}");

    public BatchFileSelectionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Discover_SelectsEveryVideoByDefaultAndUsesRelativeNames()
    {
        var nested = Path.Combine(_root, "Camera A");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(_root, "first.mp4"), "video");
        File.WriteAllText(Path.Combine(nested, "second.mov"), "video");

        var options = BatchFileSelection.Discover(_root, recursive: true);

        Assert.Equal(2, options.Count);
        Assert.All(options, option => Assert.True(option.IsSelected));
        Assert.Contains(options, option => option.DisplayName == "first.mp4");
        Assert.Contains(options, option => option.DisplayName == Path.Combine("Camera A", "second.mov"));
    }

    [Fact]
    public void SelectedFiles_ExcludesUncheckedVideos()
    {
        var first = new BatchFileOption(@"C:\Videos\first.mp4", "first.mp4");
        var second = new BatchFileOption(@"C:\Videos\second.mp4", "second.mp4") { IsSelected = false };

        var selected = BatchFileSelection.SelectedFiles([first, second]);

        Assert.Equal(first.FilePath, Assert.Single(selected));
    }

    [Fact]
    public void Summary_ReportsSelectionAndEmptyCatalog()
    {
        var options = new[]
        {
            new BatchFileOption("one.mp4", "one.mp4", 1024),
            new BatchFileOption("two.mp4", "two.mp4") { IsSelected = false },
            new BatchFileOption("three.mp4", "three.mp4", 1024)
        };

        Assert.Equal("2 of 3 selected · 2 KB · reading details…", BatchFileSelection.Summary(options));
        Assert.Equal("No supported video files found", BatchFileSelection.Summary([]));
    }

    [Fact]
    public void Discover_RespectsSubfolderChoice()
    {
        var nested = Path.Combine(_root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(_root, "root.mp4"), "video");
        File.WriteAllText(Path.Combine(nested, "nested.mp4"), "video");

        Assert.Single(BatchFileSelection.Discover(_root, recursive: false));
        Assert.Equal(2, BatchFileSelection.Discover(_root, recursive: true).Count);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
