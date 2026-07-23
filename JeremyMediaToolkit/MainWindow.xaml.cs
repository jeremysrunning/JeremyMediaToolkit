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
        _ffmpeg = ExecutableLocator.Find("ffmpeg.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"));
        _ffprobe = ExecutableLocator.Find("ffprobe.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffprobe.exe"));
        StatusText.Text = _ffmpeg is null ? "FFmpeg not found — use FFmpeg Settings" : $"FFmpeg ready: {_ffmpeg}";
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
            var files = MediaFileCatalog.Discover(InputFolder.Text, Recursive.IsChecked == true);
            if (files.Count == 0) throw new InvalidOperationException("No supported video files were found.");
            _batchProgress.StartBatch(files.Count);
            ApplyProgressState();

            var recovery = (RecoveryStrategy)RecoveryMode.SelectedIndex;
            var resolution = (OutputResolution)Resolution.SelectedIndex;
            var outputRoot = EncodingPathPlanner.OutputRoot(InputFolder.Text, resolution, recovery);
            Directory.CreateDirectory(outputRoot);
            var batchStart = Stopwatch.StartNew();
            var completed = 0;

            foreach (var input in files)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var job = EncodingPathPlanner.CreateJob(InputFolder.Text, outputRoot, input, resolution);
                var outDir = Path.GetDirectoryName(job.OutputPath)!;
                Directory.CreateDirectory(outDir);
                var output = job.OutputPath;
                _batchProgress.StartFile();
                FileProgress.Value = _batchProgress.FilePercent;
                CurrentFileText.Text = $"{completed + 1}/{files.Count}: {Path.GetFileName(input)}";
                if (SkipExisting.IsChecked == true && File.Exists(output) && new FileInfo(output).Length > 0)
                {
                    AppendLog($"Skipped existing: {output}"); completed++; UpdateBatch(completed, files.Count, batchStart); continue;
                }
                var duration = await ProbeDurationAsync(input, _cts.Token);
                var args = FfmpegCommandBuilder.Encode(input, output, SelectedLutPath!, recovery, resolution);
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


    private async Task<int> RunFfmpegProgressAsync(List<string> args, double duration, Action<double> progress, CancellationToken token)
    {
        using var process = StartProcess(_ffmpeg!, args, redirectError: true);
        var errors = new StringBuilder();
        var errTask = Task.Run(async () => { while (await process.StandardError.ReadLineAsync(token) is { } line) { errors.AppendLine(line); } }, token);
        while (await process.StandardOutput.ReadLineAsync(token) is { } line)
        {
            if (FfmpegProgressParser.TryParsePercent(line, duration, out var percent)) progress(percent);
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
        var result = await CaptureAsync(_ffprobe, FfmpegCommandBuilder.ProbeDuration(file), token);
        return double.TryParse(result.StdOut.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureProbe(); var r = await CaptureAsync(_ffprobe!, FfmpegCommandBuilder.Inspect(MediaPath.Text), CancellationToken.None); ToolsOutput.Text = r.StdOut + r.StdErr;
    });

    private async void Verify_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); ToolsOutput.Text = "Verifying every decodable frame…";
        var r = await CaptureAsync(_ffmpeg!, FfmpegCommandBuilder.Verify(MediaPath.Text), CancellationToken.None);
        var report = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_verification.csv");
        var status = r.ExitCode == 0 ? "completed" : "failed";
        File.WriteAllText(report, "file,status,exit_code,notes\r\n" + CsvFormatter.Escape(MediaPath.Text) + $",{status},{r.ExitCode}," + CsvFormatter.Escape(r.StdErr));
        ToolsOutput.Text = $"Verification {status}. Report: {report}\r\n\r\n{r.StdErr}";
    });

    private async void Rewrap_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_rewrapped.mp4");
        var r = await CaptureAsync(_ffmpeg!, FfmpegCommandBuilder.Rewrap(MediaPath.Text, output), CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async void Proxy_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_proxy.mp4");
        var r = await CaptureAsync(_ffmpeg!, FfmpegCommandBuilder.Proxy(MediaPath.Text, output), CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async void ContactSheet_Click(object sender, RoutedEventArgs e) => await ToolAction(async () =>
    {
        EnsureMedia(); EnsureFfmpeg(); var output = Path.Combine(Path.GetDirectoryName(MediaPath.Text)!, Path.GetFileNameWithoutExtension(MediaPath.Text) + "_contact-sheet.jpg");
        var r = await CaptureAsync(_ffmpeg!, FfmpegCommandBuilder.ContactSheet(MediaPath.Text, output), CancellationToken.None);
        ToolsOutput.Text = r.ExitCode == 0 ? $"Created: {output}" : r.StdErr;
    });

    private async Task ToolAction(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Media tool", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void EnsureMedia() { if (!File.Exists(MediaPath.Text)) throw new InvalidOperationException("Select a valid media file."); }
    private void EnsureFfmpeg() { if (_ffmpeg is null) throw new InvalidOperationException("FFmpeg was not found."); }
    private void EnsureProbe() { EnsureMedia(); if (_ffprobe is null) throw new InvalidOperationException("ffprobe.exe was not found beside FFmpeg or in PATH."); }

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
