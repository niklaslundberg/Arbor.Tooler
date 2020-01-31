using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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
            using (TempDirectory nugetExeDownloadDir = TempDirectory.CreateTempDirectory())
            {
                using (TempDirectory packagesTempDir = TempDirectory.CreateTempDirectory())
                {
                    using (var httpClient = new HttpClient())
                    {
                        Logger testLogger = new LoggerConfiguration().WriteTo.MySink(_output.WriteLine).MinimumLevel
                            .Verbose()
                            .CreateLogger();

                        using (Logger logger = testLogger)
                        {
                            var nugetDownloadSettings =
                                new NuGetDownloadSettings(downloadDirectory: nugetExeDownloadDir.Directory.FullName);

                            string nugetConfigFile = Path.Combine(VcsTestPathHelper.FindVcsRootPath(),
                                "src",
                                "Arbor.Tooler.Tests.Integration",
                                "testconfig",
                                "nuget.config");

                            string nugetSource = "LocalToolerTest";

                            var nugetCliSettings = new NuGetCliSettings(nugetConfigFile: nugetConfigFile,
                                nugetSourceName: nugetSource);
                            var nugetDownloadClient = new NuGetDownloadClient();

                            var installer =
                                new NuGetPackageInstaller(nugetDownloadClient,
                                    nugetCliSettings,
                                    nugetDownloadSettings,
                                    logger);

                            var nuGetPackage = new NuGetPackage(new NuGetPackageId("MyTestPackage"),
                                NuGetPackageVersion.LatestAvailable);
                            var nugetPackageSettings = new NugetPackageSettings(false,
                                nugetConfigFile: nugetConfigFile,
                                nugetSource: nugetSource);

                            DirectoryInfo installBaseDirectory = packagesTempDir.Directory;

                            NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync(
                                nuGetPackage,
                                nugetPackageSettings,
                                httpClient,
                                installBaseDirectory).ConfigureAwait(false);

                            Assert.NotNull(nuGetPackageInstallResult);
                            Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);

                            _output.WriteLine(nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());
                            _output.WriteLine(nuGetPackageInstallResult.PackageDirectory?.FullName);
                            _output.WriteLine(nuGetPackageInstallResult.NuGetPackageId?.PackageId);

                            Assert.Equal("1.0.0", nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
                        }
                    }
                }
            }
        }
    }
}