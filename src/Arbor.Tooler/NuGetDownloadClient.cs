using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Serilog;

namespace Arbor.Tooler
{
    public class NuGetDownloadClient
    {
        public async Task<NuGetDownloadResult> DownloadNuGetAsync(
            NuGetDownloadSettings nuGetDownloadSettings,
            [NotNull] ILogger logger,
            HttpClient httpClient = null,
            CancellationToken cancellationToken = default)
        {
            if (nuGetDownloadSettings == null)
            {
                throw new ArgumentNullException(nameof(nuGetDownloadSettings));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
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

            logger.Debug("Downloading {Uri} to {TempFile}", nugetExeUri, targetFileTempPath);

            using (var request = new HttpRequestMessage())
            {
                request.RequestUri = nugetExeUri;

                httpClient = httpClient ?? new HttpClient();

                using (HttpResponseMessage httpResponseMessage =
                    await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    using (var nugetExeFileStream =
                        new FileStream(targetFileTempPath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        using (Stream downloadStream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            const int defaultBufferSize = 81920;
                            await downloadStream.CopyToAsync(nugetExeFileStream, defaultBufferSize, cancellationToken).ConfigureAwait(false);

                            await nugetExeFileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                            logger.Debug("Successfully downloaded {TempFile}", targetFileTempPath);
                        }
                    }
                }
            }

            if (File.Exists(targetFile.FullName))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            logger.Debug("Copying temp file {TempFile} to target file {TargetFile}", targetFileTempPath, targetFile.FullName);

            File.Copy(targetFileTempPath, targetFile.FullName, true);

            if (File.Exists(targetFileTempPath))
            {
                File.Delete(targetFileTempPath);

                logger.Debug("Deleted temp file {TempFile}", targetFileTempPath);
            }

            return NuGetDownloadResult.Success(targetFile.FullName);
        }
    }
}
