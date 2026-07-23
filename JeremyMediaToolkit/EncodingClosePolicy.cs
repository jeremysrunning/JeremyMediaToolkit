namespace JeremyMediaToolkit;

internal static class EncodingClosePolicy
{
    public static bool ShouldResumeAfterDialog(bool wasAlreadyPaused, EncodingCloseChoice choice) =>
        choice == EncodingCloseChoice.CloseAfterCurrent || !wasAlreadyPaused;
}