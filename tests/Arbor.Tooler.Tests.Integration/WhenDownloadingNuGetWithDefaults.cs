using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration
{
    public class WhenDownloadingNuGetWithDefaults
    {
        public WhenDownloadingNuGetWithDefaults(ITestOutputHelper output) => _output = output;

        private readonly ITestOutputHelper _output;

        [Fact]
        public async Task ThenItShouldDownloadTheNuGetExeSuccessfully()
        {
            using var tempDirectory = TempDirectory.CreateTempDirectory();
            using var httpClient = new HttpClient();
            var nuGetDownloadClient = new NuGetDownloadClient();

            var nuGetDownloadSettings =
                new NuGetDownloadSettings(downloadDirectory: tempDirectory.Directory!.FullName);

            await using var logWriter = new LogStringWriter(_output.WriteLine);
            Console.SetOut(logWriter);

            await using Logger logger = new LoggerConfiguration().WriteTo.MySink(_output.WriteLine)
                .MinimumLevel.Verbose()
                .CreateLogger();

            NuGetDownloadResult nuGetDownloadResult =
                await nuGetDownloadClient.DownloadNuGetAsync(
                    nuGetDownloadSettings,
                    logger,
                    httpClient,
                    CancellationToken.None);

            _output.WriteLine(nuGetDownloadResult.Result);

            Assert.True(nuGetDownloadResult.Succeeded);
        }
    }
}