using System.IO;
using System.Text.Json;

namespace JeremyMediaToolkit;

internal sealed record AppState(string LastLutPath = "");

internal static class AppStateStore
{
    public static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JeremyMediaToolkit",
        "state.json");

    public static AppState Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppState();
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(path)) ?? new AppState();
        }
        catch (JsonException)
        {
            return new AppState();
        }
        catch (IOException)
        {
            return new AppState();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppState();
        }
    }

    public static void Save(string path, AppState state)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}
