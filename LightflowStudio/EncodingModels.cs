namespace LightflowStudio;

internal enum OutputResolution
{
    Sd480,
    Hd720,
    FullHd,
    Qhd1440,
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
