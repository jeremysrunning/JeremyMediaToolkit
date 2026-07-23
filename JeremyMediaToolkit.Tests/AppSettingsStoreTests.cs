using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"JeremyMediaToolkit-{Guid.NewGuid():N}");
    private string SettingsPath => Path.Combine(_folder, "settings.json");

    [Fact]
    public void SaveAndLoad_RoundTripsConfiguredLutFolder()
    {
        var expected = @"D:\Custom LUTs";

        AppSettingsStore.Save(SettingsPath, new AppSettings(expected));
        var actual = AppSettingsStore.Load(SettingsPath);

        Assert.Equal(expected, actual.LutFolder);
    }

    [Fact]
    public void Load_UsesDefaultFolderWhenSettingsDoNotExist()
    {
        Assert.Equal(LutCatalog.DefaultFolder, AppSettingsStore.Load(SettingsPath).LutFolder);
    }

    [Fact]
    public void Load_UsesDefaultFolderWhenSettingsAreInvalid()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "not json");

        Assert.Equal(LutCatalog.DefaultFolder, AppSettingsStore.Load(SettingsPath).LutFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
