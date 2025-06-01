using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using AwesomeAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration;

public class GetAllPackageVersionsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task GetAllPackageVersions()
    {
        var nuGetPackageInstaller = new NuGetPackageInstaller();

        Directory.SetCurrentDirectory(VcsTestPathHelper.TryFindVcsRootPath()!);

        var packageVersions = await nuGetPackageInstaller.GetAllVersions(new NuGetPackageId("Newtonsoft.Json"));

        packageVersions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllPackageVersionsDefaultConfig()
    {
        var nuGetPackageInstaller = new NuGetPackageInstaller();

        string? configFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!, "tests",
            "Arbor.Tooler.Tests.Integration", "DefaultConfig", "nuget.config");

        var packageVersions =
            await nuGetPackageInstaller.GetAllVersions(new NuGetPackageId("Newtonsoft.Json"), nugetConfig: configFile);

        packageVersions.Should().NotBeEmpty();
    }

    private void Print(NuGetConfigTreeNode node, string currentPath, int indent = 0)
    {
        string prefix = new('.', indent);

        testOutputHelper.WriteLine(prefix + node.Path + " " + node.Hops);

        foreach (var nuGetConfigTreeNode in node.Nodes.OrderByDescending(treeNode => treeNode.Hops))
        {
            Print(nuGetConfigTreeNode, currentPath, indent + 1);
        }
    }

    [Fact]
    public async Task GetConfigurationFiles()
    {
        string? currentPath = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!, "tests",
            "Arbor.Tooler.Tests.Integration", "DefaultConfig");
        var configurationFiles =
            NuGetConfigurationHelper.GetUsedConfigurationFiles(currentPath);

        configurationFiles.Should().NotBeEmpty();

        foreach (var configurationFile in configurationFiles)
        {
            //Print(configurationFile, currentPath);
        }

        foreach (string file in configurationFiles.Flatten())
        {
            testOutputHelper.WriteLine(file);
        }
    }
}