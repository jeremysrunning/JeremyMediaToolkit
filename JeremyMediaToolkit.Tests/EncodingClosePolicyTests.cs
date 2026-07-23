using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class EncodingClosePolicyTests
{
    [Theory]
    [InlineData(false, EncodingCloseChoice.KeepRunning, true)]
    [InlineData(true, EncodingCloseChoice.KeepRunning, false)]
    [InlineData(false, EncodingCloseChoice.CloseAfterCurrent, true)]
    [InlineData(true, EncodingCloseChoice.CloseAfterCurrent, true)]
    internal void ShouldResumeAfterDialog_PreservesManualPauseUnlessCurrentFileMustFinish(
        bool wasAlreadyPaused, EncodingCloseChoice choice, bool expected)
    {
        Assert.Equal(expected, EncodingClosePolicy.ShouldResumeAfterDialog(wasAlreadyPaused, choice));
    }
}