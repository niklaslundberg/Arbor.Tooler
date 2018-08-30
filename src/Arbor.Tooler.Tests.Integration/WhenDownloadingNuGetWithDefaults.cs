using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
            using (var httpClient = new HttpClient())
            {
                var nuGetDownloadClient = new NuGetDownloadClient(httpClient);

                var nuGetDownloadSettings = new NuGetDownloadSettings();

                NuGetDownloadResult nuGetDownloadResult =
                    await nuGetDownloadClient.DownloadNuGetAsync(nuGetDownloadSettings, CancellationToken.None);

                _output.WriteLine(nuGetDownloadResult.Result);

                Assert.True(nuGetDownloadResult.Succeeded);
            }
        }
    }
}