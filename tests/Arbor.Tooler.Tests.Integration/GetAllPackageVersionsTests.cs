using System.IO;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using FluentAssertions;
using Xunit;

namespace Arbor.Tooler.Tests.Integration;

public class GetAllPackageVersionsTests
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

        string? configFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!, "tests", "Arbor.Tooler.Tests.Integration", "DefaultConfig", "nuget.config");

        var packageVersions = await nuGetPackageInstaller.GetAllVersions(new NuGetPackageId("Newtonsoft.Json"), nugetConfig: configFile);

        packageVersions.Should().NotBeEmpty();
    }
}