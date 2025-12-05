using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration;

public class WhenDownloadingNuGetPackageWithDefaults(ITestOutputHelper output)
{
    [Fact]
    public async Task ItShouldHaveDownloadedTheLatestVersion()
    {
        using var nugetExeDownloadDir = TempDirectory.CreateTempDirectory();
        using var packagesTempDir = TempDirectory.CreateTempDirectory();
        using var httpClient = new HttpClient();
        Logger testLogger = new LoggerConfiguration().WriteTo.MySink(output.WriteLine).MinimumLevel
            .Verbose()
            .CreateLogger();

        await using Logger logger = testLogger;
        var nugetDownloadSettings =
            new NuGetDownloadSettings(downloadDirectory: nugetExeDownloadDir.Directory?.FullName);

        string nugetConfigFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!,
            "tests",
            "Arbor.Tooler.Tests.Integration",
            "testconfig",
            "nuget.config");

        const string nugetSource = "LocalToolerTest";

        var installer = new NuGetPackageInstaller();
        var nuGetPackage = new NuGetPackage(new NuGetPackageId("MyTestPackage"),
            NuGetPackageVersion.LatestAvailable);
        var nugetPackageSettings = new NugetPackageSettings
        {
            NugetSource = nugetSource,
            NugetConfigFile = nugetConfigFile,
            UseCli = false
        };

        DirectoryInfo? installBaseDirectory = packagesTempDir.Directory;

        NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync(
            nuGetPackage,
            nugetPackageSettings,
            httpClient,
            installBaseDirectory);

        Assert.NotNull(nuGetPackageInstallResult);
        Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);

        output.WriteLine(nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
        output.WriteLine(nuGetPackageInstallResult.PackageDirectory?.FullName);
        output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

        Assert.Equal("1.0.0", nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
    }
}