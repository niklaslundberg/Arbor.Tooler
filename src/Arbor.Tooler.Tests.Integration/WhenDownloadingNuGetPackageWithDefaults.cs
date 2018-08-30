using System.Net.Http;
using System.Threading.Tasks;
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

        private ITestOutputHelper _output;

        [Fact]
        public async Task ItShouldHaveDownloadedTheLatestVersion()
        {
            using (var httpClient = new HttpClient())
            {
                var nugetDownloadSettings = new NuGetDownloadSettings();
                var nugetCliSettings = new NuGetCliSettings();
                var nugetDownloadClient = new NuGetDownloadClient(httpClient);

                var installer =
                    new NuGetPackageInstaller(nugetCliSettings, nugetDownloadClient, nugetDownloadSettings);

                NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync(
                    new NugetPackageSettings(false),
                    new NuGetPackage(new NuGetPackageId("Arbor.X"), NuGetPackageVersion.LatestAvailable));

                _output.WriteLine(nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());

                Assert.Equal("2.2.5", nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
            }
        }
    }
}