namespace LightflowStudio;

internal static class MediaMetadataPresentation
{
    public static string Details(MediaMetadata metadata) =>
        $"{metadata.Width}×{metadata.Height} · {FormatFrameRate(metadata.FrameRate)} fps · {FormatDuration(metadata.DurationSeconds)} · {FormatSize(metadata.FileSizeBytes)}";

    public static string Tooltip(MediaMetadata metadata)
    {
        var codec = string.IsNullOrWhiteSpace(metadata.VideoCodec) ? "Unknown video codec" : metadata.VideoCodec.ToUpperInvariant();
        return $"{codec} video · {(metadata.HasAudio ? "Audio present" : "No audio stream")}";
    }

    public static string FormatDuration(double seconds) =>
        seconds <= 0 ? "Unknown length" : TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        var size = (double)value;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{value} B" : $"{size:0.#} {units[unit]}";
    }

    public static string FormatFrameRate(double frameRate) =>
        frameRate <= 0 ? "?" : frameRate.ToString(Math.Abs(frameRate - Math.Round(frameRate)) < 0.005 ? "0" : "0.##");
}
