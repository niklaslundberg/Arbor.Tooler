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
        public WhenDownloadingNuGetPackageWithDefaults(ITestOutputHelper output)
        {
            _output = output;
        }

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

                            var nugetCliSettings = new NuGetCliSettings();
                            var nugetDownloadClient = new NuGetDownloadClient();

                            var installer =
                                new NuGetPackageInstaller(nugetDownloadClient,
                                    nugetCliSettings,
                                    nugetDownloadSettings,
                                    logger: logger);

                            var nuGetPackage = new NuGetPackage(new NuGetPackageId("Arbor.X"),
                                NuGetPackageVersion.LatestAvailable);
                            var nugetPackageSettings = new NugetPackageSettings(false);

                            DirectoryInfo installBaseDirectory = packagesTempDir.Directory;

                            NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync(
                                nuGetPackage,
                                nugetPackageSettings,
                                httpClient,
                                installBaseDirectory).ConfigureAwait(false);

                            _output.WriteLine(nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
                            _output.WriteLine(nuGetPackageInstallResult.PackageDirectory.FullName);
                            _output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

                            Assert.Equal("2.2.5", nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
                        }
                    }
                }
            }
        }
    }
}
