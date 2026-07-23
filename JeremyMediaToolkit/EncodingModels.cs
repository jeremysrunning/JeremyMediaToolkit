namespace JeremyMediaToolkit;

internal enum OutputResolution
{
    FullHd,
    UltraHd,
    Source
}

internal enum RecoveryStrategy
{
    Normal,
    Salvage,
    VideoOnly
}

internal sealed record EncodingJob(string InputPath, string OutputPath);
