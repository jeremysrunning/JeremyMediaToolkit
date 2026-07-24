using System.IO;
using System.Text;

namespace LightflowStudio;

internal sealed class ActivityLogFile
{
    private readonly object _sync = new();
    private readonly long _maximumBytes;
    private readonly int _retainedFiles;

    public ActivityLogFile(string path, long maximumBytes = 5 * 1024 * 1024, int retainedFiles = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFiles);
        Path = path;
        _maximumBytes = maximumBytes;
        _retainedFiles = retainedFiles;
    }

    public string Path { get; }

    public static ActivityLogFile BesideSettings(string settingsPath) =>
        new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath)!, "activity.log"));

    public bool TryAppend(string text, DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        try
        {
            lock (_sync)
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var line = $"[{(timestamp ?? DateTimeOffset.Now):yyyy-MM-dd HH:mm:ss zzz}] {text.TrimEnd()}{Environment.NewLine}";
                RotateIfNeeded(Encoding.UTF8.GetByteCount(line));
                File.AppendAllText(Path, line, new UTF8Encoding(false));
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(Path) || new FileInfo(Path).Length + incomingBytes <= _maximumBytes) return;
        if (_retainedFiles == 0)
        {
            File.Delete(Path);
            return;
        }

        for (var index = _retainedFiles; index >= 2; index--)
        {
            var previous = $"{Path}.{index - 1}";
            if (File.Exists(previous)) File.Move(previous, $"{Path}.{index}", overwrite: true);
        }
        File.Move(Path, $"{Path}.1", overwrite: true);
    }
}
