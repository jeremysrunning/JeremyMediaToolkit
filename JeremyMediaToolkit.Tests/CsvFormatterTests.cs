using JeremyMediaToolkit;
using Xunit;

namespace JeremyMediaToolkit.Tests;

public sealed class CsvFormatterTests
{
    [Fact]
    public void Escape_AlwaysQuotesValues() => Assert.Equal("\"plain\"", CsvFormatter.Escape("plain"));

    [Fact]
    public void Escape_DoublesEmbeddedQuotes() => Assert.Equal("\"a \"\"quote\"\"\"", CsvFormatter.Escape("a \"quote\""));

    [Fact]
    public void Escape_NormalizesLineBreaks() => Assert.Equal("\"first | second third\"", CsvFormatter.Escape("first\nsecond\rthird"));
}
