namespace LightflowStudio;

internal static class MediaWarningAnalyzer
{
    public static void Apply(IReadOnlyList<BatchFileOption> options)
    {
        var readable = options.Where(option => option.Metadata is not null).ToList();
        var common = readable
            .GroupBy(option => (option.Metadata!.Width, option.Metadata.Height, FrameRate: Math.Round(option.Metadata.FrameRate, 2)))
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key.Width * group.Key.Height)
            .Select(group => group.Key)
            .FirstOrDefault();

        foreach (var option in options)
        {
            if (option.MetadataError)
            {
                option.SetWarning("DETAILS", "Media details could not be read. The file may be incomplete or unsupported.");
                continue;
            }
            if (option.Metadata is not { } metadata)
            {
                option.SetWarning("", "");
                continue;
            }

            var warnings = new List<string>();
            if (!metadata.HasAudio) warnings.Add("No audio stream was found.");
            if (metadata.Width != common.Width || metadata.Height != common.Height)
                warnings.Add($"Resolution differs from the main group ({common.Width}×{common.Height}).");
            if (Math.Abs(metadata.FrameRate - common.FrameRate) >= 0.01)
                warnings.Add($"Frame rate differs from the main group ({MediaMetadataPresentation.FormatFrameRate(common.FrameRate)} fps).");

            option.SetWarning(
                !metadata.HasAudio ? "NO AUDIO" : warnings.Count > 0 ? "DIFFERS" : "",
                string.Join(" ", warnings));
        }
    }
}
