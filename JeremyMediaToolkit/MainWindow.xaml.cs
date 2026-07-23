using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
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
    private readonly string? _commandLineFolder;
    private AppSettings _settings = new();
    private AppState _state = new();
    private Process? _activeEncodingProcess;
    private bool _closeAfterCurrent;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowAppearance.EnableDarkTitleBar(this);
        _commandLineFolder = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(Directory.Exists);
        Loaded += (_, _) =>
        {
            _settings = AppSettingsStore.Load(AppSettingsStore.SettingsPath);
            _state = AppStateStore.Load(AppStateStore.StatePath);
            PopulateSettingsControls(_settings);
            ApplySettingsToBatch(_settings);
            if (_commandLineFolder is not null) InputFolder.Text = _commandLineFolder;
            LocateTools();
            RefreshLuts();
        };
    }
    private void LocateTools()
    {
        var baseDir = AppContext.BaseDirectory;
        _ffmpeg = ExecutableLocator.Find("ffmpeg.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"), configured: _settings.FfmpegPath);
        var besideFfmpeg = _ffmpeg is null ? "" : Path.Combine(Path.GetDirectoryName(_ffmpeg)!, "ffprobe.exe");
        _ffprobe = ExecutableLocator.Find("ffprobe.exe", Path.Combine(baseDir, "ffmpeg", "bin", "ffprobe.exe"), configured: besideFfmpeg);
        StatusText.Text = _ffmpeg is null ? "FFmpeg not found — configure it in Settings" : $"FFmpeg ready: {_ffmpeg}";
    }
    private static string? PickFolder(string description, string? initialFolder = null)
    {
        using var dlg = new Forms.FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder)) dlg.SelectedPath = initialFolder;
        return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null;
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select the folder containing video files", InputFolder.Text) is { } folder) InputFolder.Text = folder;
    }

    private void RefreshLuts_Click(object sender, RoutedEventArgs e) => RefreshLuts();

    private int RefreshLuts()
    {
        var selectedPath = LutSelection.SelectedValue as string;
        var preferredPath = string.IsNullOrWhiteSpace(selectedPath) ? _state.LastLutPath : selectedPath;
        var options = LutCatalog.Discover(_settings.LutFolder);
        LutSelection.ItemsSource = options;
        LutSelection.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.FilePath, preferredPath, StringComparison.OrdinalIgnoreCase)) ?? options.FirstOrDefault();
        SettingsMessage.Text = options.Count == 0
            ? $"No .cube LUT files found in {_settings.LutFolder}"
            : $"Loaded {options.Count} LUT{(options.Count == 1 ? "" : "s")}.";
        return options.Count;
    }

    private void LutSelection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedLutPath is not { } path || string.Equals(path, _state.LastLutPath, StringComparison.OrdinalIgnoreCase)) return;
        _state = new AppState(path);
        try
        {
            AppStateStore.Save(AppStateStore.StatePath, _state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsMessage.Text = $"Could not remember LUT selection: {ex.Message}";
        }
    }

    private void BrowseDefaultVideoFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select the default video folder", SettingsDefaultVideoFolder.Text) is { } folder)
            SettingsDefaultVideoFolder.Text = folder;
    }

    private void BrowseSettingsLutFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select the folder containing .cube LUT files", SettingsLutFolder.Text) is { } folder)
            SettingsLutFolder.Text = folder;
    }

    private void BrowseSettingsFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "FFmpeg executable|ffmpeg.exe", Title = "Select ffmpeg.exe" };
        if (File.Exists(SettingsFfmpegPath.Text)) dialog.InitialDirectory = Path.GetDirectoryName(SettingsFfmpegPath.Text);
        if (dialog.ShowDialog() == true) SettingsFfmpegPath.Text = dialog.FileName;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsControls();
        if (!string.IsNullOrWhiteSpace(settings.FfmpegPath) && !File.Exists(settings.FfmpegPath))
        {
            MessageBox.Show("The configured FFmpeg executable does not exist.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            AppSettingsStore.Save(AppSettingsStore.SettingsPath, settings);
            _settings = settings;
            ApplySettingsToBatch(settings);
            LocateTools();
            var lutCount = RefreshLuts();
            SettingsMessage.Text = $"Settings saved. {lutCount} LUT{(lutCount == 1 ? "" : "s")} available.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Could not save settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        PopulateSettingsControls(new AppSettings());
        SettingsMessage.Text = "Default values loaded. Select Save Settings to apply them.";
    }

    private AppSettings ReadSettingsControls() => AppSettings.Normalize(new AppSettings
    {
        DefaultVideoFolder = SettingsDefaultVideoFolder.Text,
        LutFolder = SettingsLutFolder.Text,
        FfmpegPath = SettingsFfmpegPath.Text,
        DefaultResolution = (OutputResolution)SettingsResolution.SelectedIndex,
        DefaultRecovery = (RecoveryStrategy)SettingsRecoveryMode.SelectedIndex,
        IncludeSubfolders = SettingsRecursive.IsChecked == true,
        SkipExisting = SettingsSkipExisting.IsChecked == true
    });

    private void PopulateSettingsControls(AppSettings settings)
    {
        SettingsDefaultVideoFolder.Text = settings.DefaultVideoFolder;
        SettingsLutFolder.Text = settings.LutFolder;
        SettingsFfmpegPath.Text = settings.FfmpegPath;
        SettingsResolution.SelectedIndex = (int)settings.DefaultResolution;
        SettingsRecoveryMode.SelectedIndex = (int)settings.DefaultRecovery;
        SettingsRecursive.IsChecked = settings.IncludeSubfolders;
        SettingsSkipExisting.IsChecked = settings.SkipExisting;
    }

    private void ApplySettingsToBatch(AppSettings settings)
    {
        InputFolder.Text = settings.DefaultVideoFolder;
        Resolution.SelectedIndex = (int)settings.DefaultResolution;
        RecoveryMode.SelectedIndex = (int)settings.DefaultRecovery;
        Recursive.IsChecked = settings.IncludeSubfolders;
        SkipExisting.IsChecked = settings.SkipExisting;
    }

    private void BrowseMedia_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Video files|*.mp4;*.mov;*.mxf;*.mkv;*.avi|All files|*.*" };
        if (Directory.Exists(_settings.DefaultVideoFolder)) dialog.InitialDirectory = _settings.DefaultVideoFolder;
        if (dialog.ShowDialog() == true) MediaPath.Text = dialog.FileName;
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEncoderInputs()) return;
        _closeAfterCurrent = false;
        _cts = new CancellationTokenSource();
        ToggleEncoding(true);

        var total = 0;
        var encoded = 0;
        var failed = 0;
        var skipped = 0;
        var outputRoot = "";
        var outcome = "completed";
        Stopwatch? batchStart = null;

        try
        {
            var files = MediaFileCatalog.Discover(InputFolder.Text, Recursive.IsChecked == true);
            if (files.Count == 0) throw new InvalidOperationException("No supported video files were found.");
            total = files.Count;
            _batchProgress.StartBatch(total);
            ApplyProgressState();

            var recovery = (RecoveryStrategy)RecoveryMode.SelectedIndex;
            var resolution = (OutputResolution)Resolution.SelectedIndex;
            outputRoot = EncodingPathPlanner.OutputRoot(InputFolder.Text, resolution, recovery);
            Directory.CreateDirectory(outputRoot);
            batchStart = Stopwatch.StartNew();
            var startedAt = DateTime.Now;

            CurrentFileText.Text = $"Analyzing {total} file{(total == 1 ? "" : "s")}…";
            AppendLog($"Preparing batch — {total} file{(total == 1 ? "" : "s")} discovered.");
            var durations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                _cts.Token.ThrowIfCancellationRequested();
                durations[file] = await ProbeDurationAsync(file, _cts.Token);
            }
            var sourceDuration = TimeSpan.FromSeconds(durations.Values.Where(value => value > 0).Sum());
            AppendLog(BatchLogFormatter.Started(total, outputRoot, resolution, recovery, sourceDuration, startedAt));

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
                CurrentFileText.Text = $"{completed + 1}/{total}: {Path.GetFileName(input)}";

                if (SkipExisting.IsChecked == true && File.Exists(output) && new FileInfo(output).Length > 0)
                {
                    skipped++;
                    completed++;
                    AppendLog($"Skipped existing: {output}");
                    UpdateBatch(completed, total, batchStart);
                    if (_closeAfterCurrent)
                    {
                        outcome = "stopped after current file";
                        break;
                    }
                    continue;
                }

                var args = FfmpegCommandBuilder.Encode(input, output, SelectedLutPath!, recovery, resolution);
                var exit = await RunFfmpegProgressAsync(args, durations[input], p =>
                {
                    _batchProgress.ReportFileProgress(p);
                    FileProgress.Value = _batchProgress.FilePercent;
                }, _cts.Token);
                if (exit == 0)
                {
                    encoded++;
                    AppendLog($"Completed: {output}");
                }
                else
                {
                    failed++;
                    AppendLog($"FAILED ({exit}): {input}");
                }

                completed++;
                UpdateBatch(completed, total, batchStart);
                if (_closeAfterCurrent)
                {
                    outcome = "stopped after current file";
                    break;
                }
            }

            CurrentFileText.Text = outcome == "completed" ? "Batch complete" : "Current file complete — closing";
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            AppendLog("Encoding cancelled.");
            CurrentFileText.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            outcome = "failed";
            MessageBox.Show(ex.Message, "Encoding error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppendLog(ex.ToString());
        }
        finally
        {
            if (batchStart is not null && total > 0)
                AppendLog(BatchLogFormatter.Finished(outcome, total, encoded, failed, skipped, batchStart.Elapsed, outputRoot));

            var shouldClose = _closeAfterCurrent;
            _closeAfterCurrent = false;
            ToggleEncoding(false);
            _cts.Dispose();
            _cts = null;
            if (shouldClose)
            {
                _forceClose = true;
                _ = Dispatcher.BeginInvoke(new Action(Close));
            }
        }
    }
    private bool ValidateEncoderInputs()
    {
        if (_ffmpeg is null || !File.Exists(_ffmpeg)) { MessageBox.Show("FFmpeg was not found. Open Settings to configure ffmpeg.exe."); return false; }
        if (!Directory.Exists(InputFolder.Text)) { MessageBox.Show("Select a valid video folder."); return false; }
        if (SelectedLutPath is not { } lut || !File.Exists(lut) || !lut.EndsWith(".cube", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Select a valid .cube LUT from the LUT dropdown."); return false; }
        return true;
    }

    private string? SelectedLutPath => (LutSelection.SelectedItem as LutOption)?.FilePath;


    private async Task<int> RunFfmpegProgressAsync(List<string> args, double duration, Action<double> progress, CancellationToken token)
    {
        using var process = StartProcess(_ffmpeg!, args, redirectError: true);
        _activeEncodingProcess = process;
        try
        {
            var errors = new StringBuilder();
            var errTask = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync(token) is { } line) errors.AppendLine(line);
            }, token);
            while (await process.StandardOutput.ReadLineAsync(token) is { } line)
            {
                if (FfmpegProgressParser.TryParsePercent(line, duration, out var percent)) progress(percent);
            }
            await process.WaitForExitAsync(token);
            await errTask;
            if (process.ExitCode != 0) AppendLog(errors.ToString());
            progress(100);
            return process.ExitCode;
        }
        finally
        {
            if (ReferenceEquals(_activeEncodingProcess, process)) _activeEncodingProcess = null;
        }
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
    private void ToggleEncoding(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        BatchStateText.Text = running ? "ENCODING" : "READY";
        BatchStateText.Foreground = (System.Windows.Media.Brush)FindResource(running ? "OrangeBrush" : "SuccessBrush");
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelActiveEncoding();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_cts is null || _forceClose) return;

        e.Cancel = true;
        var dialog = new EncodingCloseDialog { Owner = this };
        dialog.ShowDialog();
        switch (dialog.Choice)
        {
            case EncodingCloseChoice.CloseAfterCurrent:
                _closeAfterCurrent = true;
                CurrentFileText.Text = "Will close after the current file finishes";
                AppendLog("Close requested — the application will close after the current file finishes.");
                break;
            case EncodingCloseChoice.CloseNow:
                _forceClose = true;
                CancelActiveEncoding();
                _ = Dispatcher.BeginInvoke(new Action(Close));
                break;
        }
    }

    private void CancelActiveEncoding()
    {
        _cts?.Cancel();
        try
        {
            if (_activeEncodingProcess is { HasExited: false } process) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the kill request.
        }
    }
    private void AppendLog(string text)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.Text = ActivityLog.Prepend(LogBox.Text, text);
            LogBox.CaretIndex = 0;
            LogBox.ScrollToHome();
        });
    }

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
