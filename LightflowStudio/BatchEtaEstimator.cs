namespace LightflowStudio;

internal static class BatchEtaEstimator
{
    public static TimeSpan? Estimate(TimeSpan elapsed, int completedFiles, int totalFiles, double currentFilePercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completedFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalFiles);
        var completedWork = Math.Clamp(completedFiles + Math.Clamp(currentFilePercent, 0, 100) / 100d, 0, totalFiles);
        if (completedWork <= 0 || elapsed <= TimeSpan.Zero) return null;
        var remainingWork = totalFiles - completedWork;
        return TimeSpan.FromTicks((long)(elapsed.Ticks * remainingWork / completedWork));
    }
}
