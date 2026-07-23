using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class MediaFileCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"LightflowStudio-{Guid.NewGuid():N}");

    public MediaFileCatalogTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Discover_FiltersSupportedExtensionsCaseInsensitively()
    {
        var supported = new[] { "a.mp4", "b.MOV", "c.mkv", "d.MXF" };
        foreach (var file in supported) File.WriteAllText(Path.Combine(_root, file), "video");
        File.WriteAllText(Path.Combine(_root, "ignore.avi"), "video");
        File.WriteAllText(Path.Combine(_root, "ignore.txt"), "text");

        var files = MediaFileCatalog.Discover(_root, recursive: false);

        Assert.Equal(supported.Order(StringComparer.OrdinalIgnoreCase), files.Select(Path.GetFileName));
    }

    [Fact]
    public void Discover_OnlyIncludesSubfoldersWhenRecursive()
    {
        var nested = Path.Combine(_root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(_root, "root.mp4"), "video");
        File.WriteAllText(Path.Combine(nested, "nested.mp4"), "video");

        Assert.Single(MediaFileCatalog.Discover(_root, recursive: false));
        Assert.Equal(2, MediaFileCatalog.Discover(_root, recursive: true).Count);
    }

    [Fact]
    public void Discover_ExcludesCurrentAndLegacyGeneratedFoldersAtAnyDepth()
    {
        var output = Path.Combine(_root, "nested", "Lightflow-1080p-LUT", "more");
        var legacyOutput = Path.Combine(_root, "nested", "Toolkit-4K-LUT");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(legacyOutput);
        File.WriteAllText(Path.Combine(output, "generated.mp4"), "video");
        File.WriteAllText(Path.Combine(legacyOutput, "legacy-generated.mp4"), "video");
        File.WriteAllText(Path.Combine(_root, "source.mp4"), "video");

        var file = Assert.Single(MediaFileCatalog.Discover(_root, recursive: true));

        Assert.Equal("source.mp4", Path.GetFileName(file));
    }

    [Fact]
    public void Discover_ExcludesConfiguredOutputTreeAndSameFolderOutputs()
    {
        var output = Path.Combine(_root, "Deliverables");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(_root, "source.mp4"), "video");
        File.WriteAllText(Path.Combine(_root, "source_1080p.mp4"), "output");
        File.WriteAllText(Path.Combine(_root, "source_Social.mp4"), "custom output");
        File.WriteAllText(Path.Combine(output, "source_4K.mp4"), "output");

        var file = Assert.Single(MediaFileCatalog.Discover(_root, recursive: true, excludedFolder: output, excludedFilenameSuffix: "_Social"));

        Assert.Equal("source.mp4", Path.GetFileName(file));
    }
    [Fact]
    public void Discover_ReturnsEmptyForMissingFolder()
    {
        Assert.Empty(MediaFileCatalog.Discover(Path.Combine(_root, "missing"), recursive: true));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
