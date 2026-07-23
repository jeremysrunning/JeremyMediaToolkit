namespace JeremyMediaToolkit;

internal static class CsvFormatter
{
    public static string Escape(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " | ") + "\"";
}
