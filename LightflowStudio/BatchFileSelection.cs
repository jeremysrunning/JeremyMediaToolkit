using System.IO;

namespace LightflowStudio;

internal static class BatchFileSelection
{
    public static IReadOnlyList<BatchFileOption> Discover(string folder, bool recursive) =>
        MediaFileCatalog.Discover(folder, recursive)
            .Select(path => new BatchFileOption(path, Path.GetRelativePath(folder, path), new FileInfo(path).Length))
            .ToList();

    public static IReadOnlyList<string> SelectedFiles(IEnumerable<BatchFileOption> options) =>
        options.Where(option => option.IsSelected).Select(option => option.FilePath).ToList();

    public static string Summary(IEnumerable<BatchFileOption> options)
    {
        var items = options.ToList();
        var selectedItems = items.Where(item => item.IsSelected).ToList();
        if (items.Count == 0) return "No supported video files found";
        var summary = $"{selectedItems.Count} of {items.Count} selected · {MediaMetadataPresentation.FormatSize(selectedItems.Sum(item => item.FileSizeBytes))}";
        if (selectedItems.Any(item => item.IsAnalyzing)) return summary + " · reading details…";
        var duration = selectedItems.Sum(item => item.Metadata?.DurationSeconds ?? 0);
        return duration > 0 ? summary + $" · {MediaMetadataPresentation.FormatDuration(duration)} total" : summary;
    }
}