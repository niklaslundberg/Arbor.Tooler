using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration
{
    public class WhenDownloadingNuGetPackageWithNullSettings
    {
        public WhenDownloadingNuGetPackageWithNullSettings(ITestOutputHelper output)
        {
            _output = output;
        }

        private ITestOutputHelper _output;

        [Fact]
        public async Task ItShouldHaveDownloadedTheLatestVersion()
        {
            var installer = new NuGetPackageInstaller();

            NuGetPackageInstallResult nuGetPackageInstallResult = await installer.InstallPackageAsync("Arbor.X").ConfigureAwait(false);

            _output.WriteLine(nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
            _output.WriteLine(nuGetPackageInstallResult.PackageDirectory.FullName);
            _output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

            Assert.Equal("2.2.5", nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
        }
    }
}
