using LightflowStudio;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class OutputDestinationPlannerTests : IDisposable
{
    private readonly string _input = Path.Combine(Path.GetTempPath(), $"Lightflow-Output-{Guid.NewGuid():N}");

    public OutputDestinationPlannerTests() => Directory.CreateDirectory(_input);

    [Fact]
    public void SameFolder_UsesInputRoot()
    {
        var root = OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.FullHd,
            new OutputDestinationOptions(OutputDestinationMode.SameFolder, "", "", ""));
        Assert.Equal(_input, root);
    }

    [Fact]
    public void BlankSubfolder_DefaultsToResolutionName()
    {
        var root = OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.UltraHd,
            new OutputDestinationOptions(OutputDestinationMode.Subfolder, "", "", ""));
        Assert.Equal(Path.Combine(_input, "4K"), root);
    }

    [Fact]
    public void NamedSubfolder_IsTrimmedAndValidated()
    {
        var root = OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.FullHd,
            new OutputDestinationOptions(OutputDestinationMode.Subfolder, "  Deliverables  ", "", ""));
        Assert.Equal(Path.Combine(_input, "Deliverables"), root);
        Assert.Throws<ArgumentException>(() => OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.FullHd,
            new OutputDestinationOptions(OutputDestinationMode.Subfolder, @"nested\folder", "", "")));
    }

    [Fact]
    public void SpecificFolder_UsesChosenAbsolutePath()
    {
        var chosen = Path.Combine(_input, "..", "Exports");
        var root = OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.Source,
            new OutputDestinationOptions(OutputDestinationMode.SpecificFolder, "", chosen, ""));
        Assert.Equal(Path.GetFullPath(chosen), root);
    }

    [Fact]
    public void SameFolderSuffix_DefaultsToResolutionAndCanBeCustomized()
    {
        Assert.Equal("_1080p", OutputDestinationPlanner.ResolveFilenameSuffix(OutputResolution.FullHd,
            new OutputDestinationOptions(OutputDestinationMode.SameFolder, "", "", "")));
        Assert.Equal("_Social", OutputDestinationPlanner.ResolveFilenameSuffix(OutputResolution.UltraHd,
            new OutputDestinationOptions(OutputDestinationMode.SameFolder, "", "", "_Social")));
        Assert.Equal("", OutputDestinationPlanner.ResolveFilenameSuffix(OutputResolution.UltraHd,
            new OutputDestinationOptions(OutputDestinationMode.Subfolder, "", "", "_Ignored")));
    }
    [Fact]
    public void SpecificFolder_RequiresAPath()
    {
        Assert.Throws<ArgumentException>(() => OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.Source,
            new OutputDestinationOptions(OutputDestinationMode.SpecificFolder, "", "  ", "")));
        Assert.Throws<ArgumentException>(() => OutputDestinationPlanner.ResolveRoot(_input, OutputResolution.Source,
            new OutputDestinationOptions(OutputDestinationMode.SpecificFolder, "", _input, "")));
    }

    public void Dispose() => Directory.Delete(_input, recursive: true);
}
