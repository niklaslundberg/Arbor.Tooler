using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration
{
    public class WhenDownloadingNuGetPackageWithDefaults
    {
        public WhenDownloadingNuGetPackageWithDefaults(ITestOutputHelper output) => _output = output;

        private readonly ITestOutputHelper _output;

        [Fact]
        public async Task ItShouldHaveDownloadedTheLatestVersion()
        {
            using var nugetExeDownloadDir = TempDirectory.CreateTempDirectory();
            using var packagesTempDir = TempDirectory.CreateTempDirectory();
            using var httpClient = new HttpClient();
            Logger testLogger = new LoggerConfiguration().WriteTo.MySink(_output.WriteLine).MinimumLevel
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

            var nugetCliSettings = new NuGetCliSettings(nugetSourceName: nugetSource,
nugetConfigFile: nugetConfigFile);
            var nugetDownloadClient = new NuGetDownloadClient();

            var installer =
                new NuGetPackageInstaller(nugetDownloadClient,
                    nugetCliSettings,
                    nugetDownloadSettings,
                    logger);

            var nuGetPackage = new NuGetPackage(new NuGetPackageId("MyTestPackage"),
                NuGetPackageVersion.LatestAvailable);
            var nugetPackageSettings = new NugetPackageSettings(false,
                nugetSource,
                nugetConfigFile);

            DirectoryInfo? installBaseDirectory = packagesTempDir.Directory;

            NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync(
                nuGetPackage,
                nugetPackageSettings,
                httpClient,
                installBaseDirectory);

            Assert.NotNull(nuGetPackageInstallResult);
            Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);

            _output.WriteLine(nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
            _output.WriteLine(nuGetPackageInstallResult.PackageDirectory?.FullName);
            _output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

            Assert.Equal("1.0.0", nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
        }
    }
}