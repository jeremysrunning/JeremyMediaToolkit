using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace JeremyMediaToolkit;

public partial class MainWindow : Window
{
    private string? _ffmpeg;
    private string? _ffprobe;
    private CancellationTokenSource? _cts;
    private readonly BatchProgressState _batchProgress = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LocateTools();
            LutFolder.Text = AppSettingsStore.Load(AppSettingsStore.SettingsPath).LutFolder;
            RefreshLuts();
        };
        if (Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(Directory.Exists) is string folder)
            InputFolder.Text = folder;
    }

    private void LocateTools()
    {
        var baseDir = AppContext.BaseDirectory;
        _ffmpeg = FindExecutable("ffmpeg.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"));
        _ffprobe = FindExecutable("ffprobe.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffprobe.exe"));
        StatusText.Text = _ffmpeg is null ? "FFmpeg not found — use FFmpeg Settings" : $"FFmpeg ready: {_ffmpeg}";
    }

    private static string? FindExecutable(string name, string bundled)
    {
        if (File.Exists(bundled)) return bundled;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator).Select(p => Path.Combine(p.Trim('"'), name)).FirstOrDefault(File.Exists);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "FFmpeg executable|ffmpeg.exe", Title = "Select ffmpeg.exe" };
        if (dlg.ShowDialog() == true)
        {
            _ffmpeg = dlg.FileName;
            var probe = Path.Combine(Path.GetDirectoryName(dlg.FileName)!, "ffprobe.exe");
            _ffprobe = File.Exists(probe) ? probe : _ffprobe;
            StatusText.Text = $"FFmpeg ready: {_ffmpeg}";
        }
    }

    private static string? PickFolder(string description)
    {
        using var dlg = new Forms.FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null;
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e) { if (PickFolder("Select the folder containing video files") is { } p) InputFolder.Text = p; }
    private void BrowseLutFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select the folder containing .cube LUT files") is not { } folder) return;
        LutFolder.Text = folder;
        AppSettingsStore.Save(AppSettingsStore.SettingsPath, new AppSettings(folder));
        RefreshLuts();
    }

    private void RefreshLuts_Click(object sender, RoutedEventArgs e) => RefreshLuts();

    private void RefreshLuts()
    {
        var selectedPath = LutSelection.SelectedValue as string;
        var options = LutCatalog.Discover(LutFolder.Text);
        LutSelection.ItemsSource = options;
        LutSelection.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? options.FirstOrDefault();
        StatusText.Text = options.Count == 0
            ? $"No .cube LUT files found in {LutFolder.Text}"
            : $"Loaded {options.Count} LUT{(options.Count == 1 ? "" : "s")}";
    }

    private void BrowseMedia_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Video files|*.mp4;*.mov;*.mxf;*.mkv;*.avi|All files|*.*" };
        if (dlg.ShowDialog() == true) MediaPath.Text = dlg.FileName;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEncoderInputs()) return;
        _cts = new CancellationTokenSource();
        ToggleEncoding(true);
        LogBox.Clear();
        try
        {
            var option = Recursive.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(InputFolder.Text, "*.*", option)
                .Where(p => new[] { ".mp4", ".mov", ".mkv", ".mxf" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Toolkit-", StringComparison.OrdinalIgnoreCase)).Order().ToList();
            if (files.Count == 0) throw new InvalidOperationException("No supported video files were found.");
            _batchProgress.StartBatch(files.Count);
            ApplyProgressState();

            var mode = RecoveryMode.SelectedIndex;
            var suffix = mode == 1 ? "-Salvage" : mode == 2 ? "-VideoOnly" : "";
            var resName = Resolution.SelectedIndex == 0 ? "1080p" : Resolution.SelectedIndex == 1 ? "4K" : "Source";
            var outputRoot = Path.Combine(InputFolder.Text, $"Toolkit-{resName}-LUT{suffix}");
            Directory.CreateDirectory(outputRoot);
            var batchStart = Stopwatch.StartNew();
            var completed = 0;

            foreach (var input in files)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var relativeDir = Path.GetDirectoryName(Path.GetRelativePath(InputFolder.Text, input)) ?? "";
                var outDir = Path.Combine(outputRoot, relativeDir);
                Directory.CreateDirectory(outDir);
                var output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(input) + $"_{resName}.mp4");
                _batchProgress.StartFile();
                FileProgress.Value = _batchProgress.FilePercent;
                CurrentFileText.Text = $"{completed + 1}/{files.Count}: {Path.GetFileName(input)}";
                if (SkipExisting.IsChecked == true && File.Exists(output) && new FileInfo(output).Length > 0)
                {
                    AppendLog($"Skipped existing: {output}"); completed++; UpdateBatch(completed, files.Count, batchStart); continue;
                }
                var duration = await ProbeDurationAsync(input, _cts.Token);
                var args = BuildEncodeArguments(input, output, SelectedLutPath!, mode);
                var exit = await RunFfmpegProgressAsync(args, duration, p =>
                {
                    _batchProgress.ReportFileProgress(p);
                    FileProgress.Value = _batchProgress.FilePercent;
                }, _cts.Token);
                if (exit == 0) AppendLog($"Completed: {output}"); else AppendLog($"FAILED ({exit}): {input}");
                completed++; UpdateBatch(completed, files.Count, batchStart);
            }
            CurrentFileText.Text = "Batch complete";
        }
        catch (OperationCanceledException) { AppendLog("Encoding cancelled."); CurrentFileText.Text = "Cancelled"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Encoding error", MessageBoxButton.OK, MessageBoxImage.Error); AppendLog(ex.ToString()); }
        finally { ToggleEncoding(false); _cts.Dispose(); _cts = null; }
    }

    private bool ValidateEncoderInputs()
    {
        if (_ffmpeg is null || !File.Exists(_ffmpeg)) { MessageBox.Show("FFmpeg was not found. Use FFmpeg Settings to select ffmpeg.exe."); return false; }
        if (!Directory.Exists(InputFolder.Text)) { MessageBox.Show("Select a valid video folder."); return false; }
        if (SelectedLutPath is not { } lut || !File.Exists(lut) || !lut.EndsWith(".cube", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Select a valid .cube LUT from the LUT dropdown."); return false; }
        return true;
    }

    private string? SelectedLutPath => (LutSelection.SelectedItem as LutOption)?.FilePath;

    private List<string> BuildEncodeArguments(string input, string output, string lut, int mode)
    {
        var a = new List<string> { "-hide_banner", "-y" };
        if (mode > 0) a.AddRange(["-fflags", "+discardcorrupt+genpts", "-err_detect", "ignore_err"]);
        a.AddRange(["-i", input, "-map", "0:v:0"]);
        if (mode != 2) a.AddRange(["-map", mode == 1 ? "0:a:0?" : "0:a?"]);
        var lutEscaped = lut.Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");
        var scale = Resolution.SelectedIndex == 0 ? ",scale=-2:1080" : Resolution.SelectedIndex == 1 ? ",scale=3840:2160:force_original_aspect_ratio=decrease,pad=3840:2160:(ow-iw)/2:(oh-ih)/2" : "";
        a.AddRange(["-vf", $"lut3d=file='{lutEscaped}'{scale}", "-c:v", "h264_nvenc", "-preset", "p7", "-tune", "hq", "-rc", "vbr", "-cq", "18", "-b:v", "0", "-multipass", "fullres", "-spatial-aq", "1", "-temporal-aq", "1", "-aq-strength", "8", "-pix_fmt", "yuv420p"]);
        if (mode == 0) a.AddRange(["-c:a", "copy"]);
        else if (mode == 1) a.AddRange(["-c:a", "aac", "-b:a", "192k", "-af", "aresample=async=1:first_pts=0"]);
        else a.Add("-an");
        a.AddRange(["-movflags", "+faststart", "-progress", "pipe:1", "-nostats", output]);
        return a;
    }

    private async Task<int> RunFfmpegProgressAsync(List<string> args, double duration, Action<double> progress, CancellationToken token)
    {
        using var process = StartProcess(_ffmpeg!, args, redirectError: true);
        var errors = new StringBuilder();
        var errTask = Task.Run(async () => { while (await process.StandardError.ReadLineAsync(token) is { } line) { errors.AppendLine(line); } }, token);
        while (await process.StandardOutput.ReadLineAsync(token) is { } line)
        {
            if (line.StartsWith("out_time_us=") && long.TryParse(line[12..], out var us) && duration > 0)
                progress(Math.Clamp(us / 1_000_000d / duration * 100, 0, 100));
        }
        await process.WaitForExitAsync(token); await errTask;
        if (process.ExitCode != 0) AppendLog(errors.ToString());
        progress(100); return process.ExitCode;
    }

    private void UpdateBatch(int completed, int total, Stopwatch sw)
    {
        _batchProgress.ReportBatchProgress(completed, total);
        BatchProgress.Value = _batchProgress.BatchPercent;
        var remaining = completed == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(sw.Elapsed.Ticks * (total - completed) / completed);
        EtaText.Text = $"Completed {completed} of {total} — estimated remaining: {remaining:hh\\:mm\\:ss}";
    }
    private void ApplyProgressState()
    {
        BatchProgress.Value = _batchProgress.BatchPercent;
        FileProgress.Value = _batchProgress.FilePercent;
        EtaText.Text = _batchProgress.StatusText;
    }
    private void ToggleEncoding(bool running) { StartButton.IsEnabled = !running; CancelButton.IsEnabled = running; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void AppendLog(string text) { Dispatcher.Invoke(() => { LogBox.AppendText(text.TrimEnd() + Environment.NewLine); LogBox.ScrollToEnd(); }); }

    private async Task<double> ProbeDurationAsync(string file, CancellationToken token)
    {
        if (_ffprobe is null) return 0;
        var result = await CaptureAsync(_ffprobe, ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", file], token);
        return double.TryParse(result.StdOut.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureProbe(); var r = await CaptureAsync(_ffprobe!, ["-hide_banner", "-show_format", "-show_streams", MediaPath.Text], CancellationToken.None); ToolsOutput.Text = r.StdOut + r.StdErr;
    });

    private async void Verify_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); ToolsOutput.Text = "Verifying every decodable frame…";
        var r = await CaptureAsync(_ffmpeg!, ["-v", "warning", "-i", MediaPath.Text, "-map", "0:v:0", "-f", "null", "NUL"], CancellationToken.None);
        var report = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_verification.csv");
        var status = r.ExitCode == 0 ? "completed" : "failed";
        File.WriteAllText(report, "file,status,exit_code,notes\r\n" + Csv(MediaPath.Text) + $",{status},{r.ExitCode}," + Csv(r.StdErr));
        ToolsOutput.Text = $"Verification {status}. Report: {report}\r\n\r\n{r.StdErr}";
    });

    private async void Rewrap_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_rewrapped.mp4");
        var r = await CaptureAsync(_ffmpeg!, ["-hide_banner", "-y", "-i", MediaPath.Text, "-map", "0", "-c", "copy", "-movflags", "+faststart", output], CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async void Proxy_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_proxy.mp4");
        var r = await CaptureAsync(_ffmpeg!, ["-hide_banner", "-y", "-i", MediaPath.Text, "-vf", "scale=-2:1080", "-c:v", "h264_nvenc", "-preset", "p4", "-cq", "24", "-b:v", "0", "-c:a", "aac", "-b:a", "128k", output], CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async void ContactSheet_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_contact-sheet.jpg");
        var filter = "fps=1/10,scale=480:-1,tile=4x4:padding=8:margin=8";
        var r = await CaptureAsync(_ffmpeg!, ["-hide_banner", "-y", "-i", MediaPath.Text, "-vf", filter, "-frames:v", "1", output], CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async Task ToolAction(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Media tool", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void EnsureMedia() { if (!File.Exists(MediaPath.Text)) throw new InvalidOperationException("Select a valid media file."); }
    private void EnsureFfmpeg() { if (_ffmpeg is null) throw new InvalidOperationException("FFmpeg was not found."); }
    private void EnsureProbe() { EnsureMedia(); if (_ffprobe is null) throw new InvalidOperationException("ffprobe.exe was not found beside FFmpeg or in PATH."); }
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " | ") + "\"";

    private void OpenPremiere_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PremiereHelper");
        if (!Directory.Exists(path)) path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PremiereHelper"));
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        else MessageBox.Show("PremiereHelper folder not found. It is included at the package root.");
    }

    private static Process StartProcess(string exe, IEnumerable<string> args, bool redirectError)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = redirectError };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}.");
    }
    private static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureAsync(string exe, IEnumerable<string> args, CancellationToken token)
    {
        using var p = StartProcess(exe, args, true);
        var stdout = p.StandardOutput.ReadToEndAsync(token); var stderr = p.StandardError.ReadToEndAsync(token);
        await p.WaitForExitAsync(token); return (p.ExitCode, await stdout, await stderr);
    }
}
