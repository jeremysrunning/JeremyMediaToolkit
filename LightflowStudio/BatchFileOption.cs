using System.ComponentModel;

namespace LightflowStudio;

internal sealed class BatchFileOption : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _detailsText;
    private string _detailsTooltip = "";
    private string _warningLabel = "";
    private string _warningTooltip = "";

    public BatchFileOption(string filePath, string displayName, long fileSizeBytes = 0)
    {
        FilePath = filePath;
        DisplayName = displayName;
        FileSizeBytes = fileSizeBytes;
        _detailsText = $"Reading details… · {MediaMetadataPresentation.FormatSize(fileSizeBytes)}";
    }

    public string FilePath { get; }
    public string DisplayName { get; }
    public long FileSizeBytes { get; }
    public MediaMetadata? Metadata { get; private set; }
    public bool MetadataError { get; private set; }
    public bool IsAnalyzing => Metadata is null && !MetadataError;
    public string DetailsText { get => _detailsText; private set => SetField(ref _detailsText, value); }
    public string DetailsTooltip { get => _detailsTooltip; private set => SetField(ref _detailsTooltip, value); }
    public string WarningLabel { get => _warningLabel; private set => SetField(ref _warningLabel, value); }
    public string WarningTooltip { get => _warningTooltip; private set => SetField(ref _warningTooltip, value); }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningLabel);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public void ApplyMetadata(MediaMetadata metadata)
    {
        Metadata = metadata;
        MetadataError = false;
        DetailsText = MediaMetadataPresentation.Details(metadata);
        DetailsTooltip = MediaMetadataPresentation.Tooltip(metadata);
        OnPropertyChanged(nameof(IsAnalyzing));
    }

    public void MarkMetadataUnavailable()
    {
        Metadata = null;
        MetadataError = true;
        DetailsText = $"Details unavailable · {MediaMetadataPresentation.FormatSize(FileSizeBytes)}";
        DetailsTooltip = "FFprobe could not read media details.";
        OnPropertyChanged(nameof(IsAnalyzing));
    }

    public void SetWarning(string label, string tooltip)
    {
        WarningLabel = label;
        WarningTooltip = tooltip;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (field == value) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}