using System;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration
{
    public class WhenDownloadingNuGetPackageWithNullSettings
    {
        public WhenDownloadingNuGetPackageWithNullSettings(ITestOutputHelper output) => _output = output;

        private readonly ITestOutputHelper _output;

        [Fact]
        public async Task ItShouldHaveDownloadedTheLatestVersion()
        {
            using Logger testLogger = new LoggerConfiguration().WriteTo.Debug().WriteTo.MySink(_output.WriteLine)
                .MinimumLevel
                .Verbose()
                .CreateLogger();

            var installer = new NuGetPackageInstaller(logger: testLogger);

            NuGetPackageInstallResult nuGetPackageInstallResult =
                await installer.InstallPackageAsync("Arbor.Xdt").ConfigureAwait(false);

            Assert.NotNull(nuGetPackageInstallResult);
            Assert.NotNull(nuGetPackageInstallResult.SemanticVersion);
            Assert.Equal("0.2.4", nuGetPackageInstallResult.SemanticVersion?.ToNormalizedString());

            _output.WriteLine(nuGetPackageInstallResult.SemanticVersion.ToNormalizedString());
            _output.WriteLine(nuGetPackageInstallResult.PackageDirectory.FullName);
            _output.WriteLine(nuGetPackageInstallResult.NuGetPackageId.PackageId);

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}