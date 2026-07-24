using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class ActivityLogFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"LightflowStudio-log-{Guid.NewGuid():N}");
    private string LogPath => Path.Combine(_folder, "activity.log");

    [Fact]
    public void BesideSettings_UsesTheSettingsDirectory()
    {
        var log = ActivityLogFile.BesideSettings(Path.Combine(_folder, "settings.json"));

        Assert.Equal(LogPath, log.Path);
    }

    [Fact]
    public void TryAppend_WritesTimestampedActivity()
    {
        var log = new ActivityLogFile(LogPath);

        Assert.True(log.TryAppend("Encoding started.", new DateTimeOffset(2026, 7, 23, 12, 34, 56, TimeSpan.FromHours(-7))));
        Assert.Contains("[2026-07-23 12:34:56 -07:00] Encoding started.", File.ReadAllText(LogPath));
    }

    [Fact]
    public void TryAppend_RotatesAndRetainsConfiguredHistory()
    {
        var log = new ActivityLogFile(LogPath, maximumBytes: 80, retainedFiles: 2);

        log.TryAppend(new string('A', 50));
        log.TryAppend(new string('B', 50));
        log.TryAppend(new string('C', 50));

        Assert.True(File.Exists(LogPath));
        Assert.True(File.Exists($"{LogPath}.1"));
        Assert.True(File.Exists($"{LogPath}.2"));
        Assert.False(File.Exists($"{LogPath}.3"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
