using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Arbor.Tooler
{
    public class NuGetDownloadClient
    {
        private readonly HttpClient _httpClient;

        public NuGetDownloadClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<NuGetDownloadResult> DownloadNuGetAsync(
            NuGetDownloadSettings nuGetDownloadSettings,
            CancellationToken cancellationToken)
        {
            if (nuGetDownloadSettings == null)
            {
                throw new ArgumentNullException(nameof(nuGetDownloadSettings));
            }

            if (!nuGetDownloadSettings.NugetDownloadEnabled)
            {
                return NuGetDownloadResult.Disabled;
            }

            if (nuGetDownloadSettings.NugetDownloadUriFormat is null)
            {
                return NuGetDownloadResult.MissingNuGetDownloadUriFormat;
            }

            if (string.IsNullOrWhiteSpace(nuGetDownloadSettings.NugetExeVersion))
            {
                return NuGetDownloadResult.MissingNuGetExeVersion;
            }

            string downloadUriFormat =
                nuGetDownloadSettings.NugetDownloadUriFormat.WithDefault(NuGetDownloadSettings
                    .DefaultNuGetExeDownloadUriFormat);

            string downloadUri = downloadUriFormat.IndexOf("{0}", StringComparison.OrdinalIgnoreCase) >= 0
                ? string.Format(downloadUriFormat, nuGetDownloadSettings.NugetExeVersion)
                : downloadUriFormat;

            if (!Uri.TryCreate(downloadUri, UriKind.Absolute, out Uri nugetExeUri)
                || !nugetExeUri.IsHttpOrHttps())
            {
                return NuGetDownloadResult.InvalidDownloadUri(downloadUri);
            }

            string downloadDirectoryPath = nuGetDownloadSettings.DownloadDirectory.WithDefault(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tools", "nuget"));

            var downloadDirectory = new DirectoryInfo(downloadDirectoryPath);

            try
            {
                if (!downloadDirectory.Exists)
                {
                    downloadDirectory.Create();
                }
            }
            catch (Exception ex)
            {
                return NuGetDownloadResult.FromException(ex);
            }

            var targetFile = new FileInfo(Path.Combine(downloadDirectory.FullName, "nuget.exe"));

            if (targetFile.Exists)
            {
                //TODO add update check
                return NuGetDownloadResult.Success(targetFile.FullName);
            }

            string targetFileTempPath = Path.Combine(downloadDirectory.FullName,
                $"nuget.exe-{DateTime.UtcNow.Ticks}.tmp");

            using (var request = new HttpRequestMessage())
            {
                request.RequestUri = nugetExeUri;

                using (HttpResponseMessage httpResponseMessage =
                    await _httpClient.SendAsync(request, cancellationToken))
                {
                    using (var nugetExeFileStream =
                        new FileStream(targetFileTempPath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        using (Stream downloadStream = await httpResponseMessage.Content.ReadAsStreamAsync())
                        {
                            const int defaultBufferSize = 81920;
                            await downloadStream.CopyToAsync(nugetExeFileStream, defaultBufferSize, cancellationToken);

                            await nugetExeFileStream.FlushAsync(cancellationToken);
                        }
                    }
                }
            }

            if (File.Exists(targetFile.FullName))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            File.Copy(targetFileTempPath, targetFile.FullName, true);

            if (File.Exists(targetFileTempPath))
            {
                File.Delete(targetFileTempPath);
            }

            return NuGetDownloadResult.Success(targetFile.FullName);
        }

        public static NuGetDownloadClient CreateDefault()
        {
            return new NuGetDownloadClient(new HttpClient());
        }
    }
}
