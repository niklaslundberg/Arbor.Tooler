using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration
{
    public class WhenDownloadingNuGetWithDefaults
    {
        public WhenDownloadingNuGetWithDefaults(ITestOutputHelper output)
        {
            _output = output;
        }

        private readonly ITestOutputHelper _output;

        [Fact]
        public async Task ThenItShouldDownloadTheNuGetExeSuccessfully()
        {
            using (TempDirectory tempDirectory = TempDirectory.CreateTempDirectory())
            {
                using (var httpClient = new HttpClient())
                {
                    var nuGetDownloadClient = new NuGetDownloadClient();

                    var nuGetDownloadSettings =
                        new NuGetDownloadSettings(downloadDirectory: tempDirectory.Directory.FullName);

                    NuGetDownloadResult nuGetDownloadResult =
                        await nuGetDownloadClient.DownloadNuGetAsync(
                            nuGetDownloadSettings,
                            Logger.None,
                            httpClient,
                            CancellationToken.None).ConfigureAwait(false);

                    _output.WriteLine(nuGetDownloadResult.Result);

                    Assert.True(nuGetDownloadResult.Succeeded);
                }
            }
        }
    }
}
