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

    [Fact]
    public void BatchOptions_ArePlacedBesideTheInputsTheyAffect()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));
        var ns = document.Root!.Name.Namespace;
        var recursive = Named(document, "Recursive");
        var inputFolder = Named(document, "InputFolder");
        var overwrite = Named(document, "OverwriteExisting");
        var outputMode = Named(document, "OutputMode");

        Assert.Equal(inputFolder.Parent!.Parent, recursive.Parent);
        Assert.Equal(outputMode.Parent!.Parent, overwrite.Parent!.Parent);
        Assert.Equal(overwrite.Parent, Named(document, "PreserveFolderStructure").Parent);
        Assert.DoesNotContain(document.Descendants(ns + "CheckBox"),
            element => (string?)element.Attribute("Content") == "Skip completed files");
    }

    [Fact]
    public void StartEncoding_IsDisabledUntilBatchRequirementsAreMet()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));

        Assert.Equal("False", (string?)Named(document, "StartButton").Attribute("IsEnabled"));
    }

    [Fact]
    public void BatchFileTable_ConstrainsRowsToTheVisibleColumnWidth()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));
        var list = Named(document, "BatchFileList");

        Assert.Equal("Disabled", list.Attributes()
            .Single(attribute => attribute.Name.LocalName == "ScrollViewer.HorizontalScrollBarVisibility").Value);
        Assert.Equal("Stretch", (string?)list.Attribute("HorizontalContentAlignment"));
    }

    [Fact]
    public void MainWindow_ProvidesEnoughWidthForTheBatchFileColumns()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "LightflowStudio", "MainWindow.xaml"));

        Assert.True(double.Parse((string?)document.Root!.Attribute("MinWidth") ?? "0") >= 1120);
    }
    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == name));
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
