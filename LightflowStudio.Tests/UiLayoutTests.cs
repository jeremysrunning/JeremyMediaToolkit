using System.Xml.Linq;
using Xunit;

namespace LightflowStudio.Tests;

public class UiLayoutTests
{
    [Fact]
    public void ActivityLog_IsCollapsedByDefault()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));
        var ns = document.Root!.Name.Namespace;
        var expander = document.Descendants(ns + "Expander")
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "ActivityLogExpander"));

        Assert.Equal("False", (string?)expander.Attribute("IsExpanded"));
    }
    [Fact]
    public void BatchSetup_DirectChildrenOnlyUseDefinedRows()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));
        var ns = document.Root!.Name.Namespace;
        var heading = document.Descendants(ns + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "Batch Setup");
        var grid = heading.Parent!;
        var rowCount = grid.Element(ns + "Grid.RowDefinitions")!.Elements(ns + "RowDefinition").Count();
        var assignedRows = grid.Elements()
            .Select(element => (string?)element.Attribute("Grid.Row"))
            .Where(value => int.TryParse(value, out _))
            .Select(value => int.Parse(value!))
            .ToList();

        Assert.NotEmpty(assignedRows);
        Assert.True(assignedRows.Max() < rowCount,
            $"Batch Setup assigns a child to row {assignedRows.Max()}, but only {rowCount} rows are defined.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find the Lightflow Studio repository root.");
    }
}
