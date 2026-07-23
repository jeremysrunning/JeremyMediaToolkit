using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class AppStateStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"JeremyMediaToolkit-{Guid.NewGuid():N}");
    private string StatePath => Path.Combine(_folder, "state.json");

    [Fact]
    public void SaveAndLoad_RoundTripsLastLutPath()
    {
        var expected = new AppState(@"D:\LUTs\Selected.cube");

        AppStateStore.Save(StatePath, expected);

        Assert.Equal(expected, AppStateStore.Load(StatePath));
    }

    [Fact]
    public void Load_ReturnsEmptyStateForMissingOrMalformedFile()
    {
        Assert.Equal(new AppState(), AppStateStore.Load(StatePath));
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StatePath, "not json");
        Assert.Equal(new AppState(), AppStateStore.Load(StatePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
