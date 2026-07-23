using System.Windows;

namespace JeremyMediaToolkit;

internal enum EncodingCloseChoice
{
    KeepRunning,
    CloseNow,
    CloseAfterCurrent
}

public partial class EncodingCloseDialog : Window
{
    internal EncodingCloseChoice Choice { get; private set; } = EncodingCloseChoice.KeepRunning;

    public EncodingCloseDialog()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowAppearance.EnableDarkTitleBar(this);
    }

    private void KeepRunning_Click(object sender, RoutedEventArgs e) => Close();

    private void CloseNow_Click(object sender, RoutedEventArgs e)
    {
        Choice = EncodingCloseChoice.CloseNow;
        Close();
    }

    private void CloseAfterCurrent_Click(object sender, RoutedEventArgs e)
    {
        Choice = EncodingCloseChoice.CloseAfterCurrent;
        Close();
    }
}
