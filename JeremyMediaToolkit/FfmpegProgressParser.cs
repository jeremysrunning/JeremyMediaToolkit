namespace JeremyMediaToolkit;

internal static class FfmpegProgressParser
{
    public static bool TryParsePercent(string line, double durationSeconds, out double percent)
    {
        percent = 0;
        if (durationSeconds <= 0 || !line.StartsWith("out_time_us=", StringComparison.Ordinal) ||
            !long.TryParse(line[12..], out var microseconds)) return false;
        percent = Math.Clamp(microseconds / 1_000_000d / durationSeconds * 100, 0, 100);
        return true;
    }
}
