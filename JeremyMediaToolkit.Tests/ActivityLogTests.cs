using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class ActivityLogTests
{
    [Fact]
    public void Append_PutsNewestEntryLast()
    {
        var text = ActivityLog.Append("Older entry", "Newest entry");

        Assert.Equal($"Older entry{Environment.NewLine}Newest entry", text);
    }

    [Fact]
    public void Append_TrimsTrailingLineBreaks()
    {
        Assert.Equal("Entry", ActivityLog.Append("", "Entry\r\n"));
    }

    [Fact]
    public void Append_IgnoresBlankEntries()
    {
        Assert.Equal("Existing", ActivityLog.Append("Existing", "\r\n"));
    }
}
