namespace JeremyMediaToolkit;

internal static class FfmpegCommandBuilder
{
    public static List<string> Encode(string input, string output, string lut, RecoveryStrategy recovery, OutputResolution resolution)
    {
        if (!Enum.IsDefined(recovery)) throw new ArgumentOutOfRangeException(nameof(recovery));
        if (!Enum.IsDefined(resolution)) throw new ArgumentOutOfRangeException(nameof(resolution));
        var args = new List<string> { "-hide_banner", "-y" };
        if (recovery != RecoveryStrategy.Normal)
            args.AddRange(["-fflags", "+discardcorrupt+genpts", "-err_detect", "ignore_err"]);
        args.AddRange(["-i", input, "-map", "0:v:0"]);
        if (recovery != RecoveryStrategy.VideoOnly)
            args.AddRange(["-map", recovery == RecoveryStrategy.Salvage ? "0:a:0?" : "0:a?"]);

        var scale = resolution switch
        {
            OutputResolution.FullHd => ",scale=-2:1080",
            OutputResolution.UltraHd => ",scale=3840:2160:force_original_aspect_ratio=decrease,pad=3840:2160:(ow-iw)/2:(oh-ih)/2",
            OutputResolution.Source => "",
            _ => throw new ArgumentOutOfRangeException(nameof(resolution))
        };
        args.AddRange(["-vf", $"lut3d=file='{EscapeFilterPath(lut)}'{scale}", "-c:v", "h264_nvenc", "-preset", "p7", "-tune", "hq", "-rc", "vbr", "-cq", "18", "-b:v", "0", "-multipass", "fullres", "-spatial-aq", "1", "-temporal-aq", "1", "-aq-strength", "8", "-pix_fmt", "yuv420p"]);

        if (recovery == RecoveryStrategy.Normal) args.AddRange(["-c:a", "copy"]);
        else if (recovery == RecoveryStrategy.Salvage) args.AddRange(["-c:a", "aac", "-b:a", "192k", "-af", "aresample=async=1:first_pts=0"]);
        else args.Add("-an");
        args.AddRange(["-movflags", "+faststart", "-progress", "pipe:1", "-nostats", output]);
        return args;
    }

    public static List<string> ProbeDuration(string file) => ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", file];
    public static List<string> Inspect(string file) => ["-hide_banner", "-show_format", "-show_streams", file];
    public static List<string> Verify(string file) => ["-v", "warning", "-i", file, "-map", "0:v:0", "-f", "null", "NUL"];
    public static List<string> Rewrap(string input, string output) => ["-hide_banner", "-y", "-i", input, "-map", "0", "-c", "copy", "-movflags", "+faststart", output];
    public static List<string> Proxy(string input, string output) => ["-hide_banner", "-y", "-i", input, "-vf", "scale=-2:1080", "-c:v", "h264_nvenc", "-preset", "p4", "-cq", "24", "-b:v", "0", "-c:a", "aac", "-b:a", "128k", output];
    public static List<string> ContactSheet(string input, string output) => ["-hide_banner", "-y", "-i", input, "-vf", "fps=1/10,scale=480:-1,tile=4x4:padding=8:margin=8", "-frames:v", "1", output];

    internal static string EscapeFilterPath(string path) => path.Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");
}
