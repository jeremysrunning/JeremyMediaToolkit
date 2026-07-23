using System.IO;
using System.Text.Json;

namespace JeremyMediaToolkit;

internal sealed record AppSettings(string LutFolder);

internal static class AppSettingsStore
{
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JeremyMediaToolkit",
        "settings.json");

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings(LutCatalog.DefaultFolder);
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            return string.IsNullOrWhiteSpace(settings?.LutFolder)
                ? new AppSettings(LutCatalog.DefaultFolder)
                : settings;
        }
        catch (JsonException)
        {
            return new AppSettings(LutCatalog.DefaultFolder);
        }
        catch (IOException)
        {
            return new AppSettings(LutCatalog.DefaultFolder);
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings(LutCatalog.DefaultFolder);
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
