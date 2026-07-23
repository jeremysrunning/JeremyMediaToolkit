using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class ExecutableLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"JeremyMediaToolkit-{Guid.NewGuid():N}");

    public ExecutableLocatorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Find_PrefersBundledExecutable()
    {
        var bundled = CreateExecutable(_root, "bundled.exe");
        var pathFolder = Directory.CreateDirectory(Path.Combine(_root, "path")).FullName;
        CreateExecutable(pathFolder, "ffmpeg.exe");

        Assert.Equal(bundled, ExecutableLocator.Find("ffmpeg.exe", bundled, pathFolder));
    }

    [Fact]
    public void Find_UsesFirstExecutableOnPathAndHandlesQuotedEntries()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "first folder")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        var expected = CreateExecutable(first, "ffmpeg.exe");
        CreateExecutable(second, "ffmpeg.exe");
        var environment = $"\"{first}\"{Path.PathSeparator}{second}";

        Assert.Equal(expected, ExecutableLocator.Find("ffmpeg.exe", Path.Combine(_root, "missing.exe"), environment));
    }

    [Fact]
    public void Find_ReturnsNullWhenExecutableIsUnavailable()
    {
        Assert.Null(ExecutableLocator.Find("ffmpeg.exe", Path.Combine(_root, "missing.exe"), _root));
    }

    private static string CreateExecutable(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "executable");
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
