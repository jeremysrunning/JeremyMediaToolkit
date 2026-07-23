using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace LightflowStudio.Tests;

public sealed class ReleasePackagingTests
{
    [Fact]
    public void FfmpegManifest_IsPinnedAndHasAValidSha256()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathAtRoot("dependencies", "ffmpeg.json")));
        var root = document.RootElement;

        Assert.DoesNotContain("latest", root.GetProperty("downloadUrl").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("autobuild-", root.GetProperty("releaseTag").GetString());
        Assert.Matches("^[a-f0-9]{64}$", root.GetProperty("sha256").GetString()!);
        Assert.Equal("LGPL-2.1-or-later", root.GetProperty("license").GetString());
        Assert.StartsWith("https://", root.GetProperty("sourceUrl").GetString());
    }

    [Fact]
    public void ReleaseWorkflow_GatesPackagingOnTests()
    {
        var workflow = File.ReadAllText(PathAtRoot(".github", "workflows", "ci-release.yml"));

        Assert.Contains("needs: test", workflow);
        Assert.Contains("dotnet test", workflow);
        Assert.Contains("Build-Release.ps1", workflow);
        Assert.Contains("GITHUB_REF_NAME", workflow);
        Assert.Contains("SHA256SUMS.txt", File.ReadAllText(PathAtRoot("scripts", "Build-Release.ps1")));
    }

    [Fact]
    public void Installer_RecursivelyPackagesTheStagedApplication()
    {
        var installer = File.ReadAllText(PathAtRoot("installer", "LightflowStudio.iss"));

        Assert.Contains("recursesubdirs", installer);
        Assert.Contains("PrivilegesRequired=lowest", installer);
        Assert.Contains("THIRD-PARTY-NOTICES.md", installer);
    }

    [Fact]
    public void ProductVersion_RemainsTheReleaseScriptAuthority()
    {
        var props = XDocument.Load(PathAtRoot("Directory.Build.props"));
        var version = props.Descendants("VersionPrefix").Single().Value;
        var releaseScript = File.ReadAllText(PathAtRoot("scripts", "Build-Release.ps1"));

        Assert.Contains("Directory.Build.props", releaseScript);
        Assert.Contains("VersionPrefix", releaseScript);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    private static string PathAtRoot(params string[] parts) =>
        Path.Combine(new[] { FindRepositoryRoot() }.Concat(parts).ToArray());

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
