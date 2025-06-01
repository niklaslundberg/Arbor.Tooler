using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Versioning;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration;

public class WhenDownloadingNuGetPackageWithNullSettings(ITestOutputHelper output)
{
    [Fact]
    public async Task ItShouldHaveDownloadedTheLatestVersion()
    {
        await using Logger testLogger = new LoggerConfiguration().WriteTo.Debug().WriteTo.MySink(output.WriteLine)
            .MinimumLevel
            .Verbose()
            .CreateLogger();

        var installer = new NuGetPackageInstaller(logger: testLogger);

        var nuGetPackage = new NuGetPackage(new NuGetPackageId("Arbor.Tooler"));
        var nugetPackageSettings = new NugetPackageSettings { UseCli = true };

        NuGetPackageInstallResult nuGetPackageInstallResult =
            await installer.InstallPackageAsync(nuGetPackage, nugetPackageSettings);

        Assert.NotNull(nuGetPackageInstallResult);
        Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);

        var minVersion = new SemanticVersion(0, 19, 0);
        nuGetPackageInstallResult.SemanticVersion.Should().BeGreaterThanOrEqualTo(minVersion);

        output.WriteLine(nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
        output.WriteLine(nuGetPackageInstallResult.PackageDirectory?.FullName);
        output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);
    }

    [Fact]
    public async Task ItShouldHaveDownloadedTheLatestVersionOfArborTooler()
    {
        await using Logger testLogger = new LoggerConfiguration().WriteTo.Debug().WriteTo.MySink(output.WriteLine)
            .MinimumLevel
            .Verbose()
            .CreateLogger();

        var installer = new NuGetPackageInstaller(logger: testLogger);

        var nuGetPackage = new NuGetPackage(new NuGetPackageId("Arbor.Tooler"));
        var nugetPackageSettings = new NugetPackageSettings { UseCli = false, AllowPreRelease = true };

        NuGetPackageInstallResult nuGetPackageInstallResult =
            await installer.InstallPackageAsync(nuGetPackage, nugetPackageSettings);

        Assert.NotNull(nuGetPackageInstallResult);
        Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);

        var minVersion = new SemanticVersion(0, 19, 0);
        nuGetPackageInstallResult.SemanticVersion.Should().BeGreaterThanOrEqualTo(minVersion);

        output.WriteLine(nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
        output.WriteLine(nuGetPackageInstallResult.PackageDirectory?.FullName);
        output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

        nuGetPackageInstallResult.PackageDirectory.Should().NotBeNull();
        nuGetPackageInstallResult.PackageDirectory!.Exists.Should().BeTrue();
        nuGetPackageInstallResult.PackageDirectory.GetFiles("Arbor.Tooler.nupkg").Should().ContainSingle();
    }
}