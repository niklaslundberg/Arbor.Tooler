using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using NuGet.Versioning;
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

            bool ownsClient = httpClient is null;

            httpClient = httpClient ?? new HttpClient();

            try
            {
                string downloadDirectoryPath = nuGetDownloadSettings.DownloadDirectory.WithDefault(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Arbor.Tooler",
                        "tools",
                        "nuget"));

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

                string targetFileTempPath = Path.Combine(downloadDirectory.FullName,
                    $"nuget.exe-{DateTime.UtcNow.Ticks}.tmp");

                if (targetFile.Exists)
                {
                    logger.Debug("Found existing nuget.exe at {FilePath}, skipping download", targetFile);

                    if (nuGetDownloadSettings.UpdateEnabled)
                    {
                        NuGetDownloadResult nuGetDownloadResult = await EnsureLatestAsync(targetFile,
                            targetFileTempPath,
                            logger,
                            httpClient,
                            cancellationToken);

                        if (nuGetDownloadResult?.Succeeded == true)
                        {
                            return nuGetDownloadResult;
                        }
                    }

                    return NuGetDownloadResult.Success(targetFile.FullName);
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

                NuGetDownloadResult result = await DownloadAsync(logger,
                    nugetExeUri,
                    targetFile,
                    targetFileTempPath,
                    httpClient,
                    cancellationToken);

                if (result.Succeeded)
                {
                    if (nuGetDownloadSettings.UpdateEnabled)
                    {
                        NuGetDownloadResult nuGetDownloadResult = await EnsureLatestAsync(targetFile,
                            targetFileTempPath,
                            logger,
                            httpClient,
                            cancellationToken);

                        if (nuGetDownloadResult?.Succeeded == true)
                        {
                            return nuGetDownloadResult;
                        }
                    }
                }

                return result;
            }
            finally
            {
                if (ownsClient)
                {
                    httpClient.Dispose();
                }
            }
        }

        private async Task<NuGetDownloadResult> DownloadAsync(
            ILogger logger,
            Uri nugetExeUri,
            FileInfo targetFile,
            string targetFileTempPath,
            HttpClient httpClient,
            CancellationToken cancellationToken)
        {
            logger.Debug("Downloading {Uri} to {TempFile}", nugetExeUri, targetFileTempPath);

            using (var request = new HttpRequestMessage())
            {
                request.RequestUri = nugetExeUri;

                bool owsClient = httpClient is null;

                httpClient = httpClient ?? new HttpClient();

                try
                {
                    using (HttpResponseMessage httpResponseMessage =
                        await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        using (var nugetExeFileStream =
                            new FileStream(targetFileTempPath, FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            using (Stream downloadStream =
                                await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                const int defaultBufferSize = 8192;
                                await downloadStream.CopyToAsync(nugetExeFileStream,
                                        defaultBufferSize,
                                        cancellationToken)
                                    .ConfigureAwait(false);

                                await nugetExeFileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                                logger.Debug("Successfully downloaded {TempFile}", targetFileTempPath);
                            }
                        }
                    }

                }
                finally
                {
                    if (owsClient)
                    {
                        httpClient.Dispose();
                    }
                }

            }

            if (File.Exists(targetFile.FullName))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            logger.Debug("Copying temp file {TempFile} to target file {TargetFile}",
                targetFileTempPath,
                targetFile.FullName);

            File.Copy(targetFileTempPath, targetFile.FullName, true);

            if (File.Exists(targetFileTempPath))
            {
                File.Delete(targetFileTempPath);

                logger.Debug("Deleted temp file {TempFile}", targetFileTempPath);
            }

            return NuGetDownloadResult.Success(targetFile.FullName);
        }

        private async Task<NuGetDownloadResult> EnsureLatestAsync(
            FileInfo targetFile,
            string targetFileTempPath,
            ILogger logger,
            HttpClient httpClient,
            CancellationToken cancellationToken)
        {
            targetFile.Refresh();

            if (!targetFile.Exists)
            {
                logger.Warning("The target nuget.exe file '{TargetFile}' does not exist, skipping latest check", targetFile.FullName);
                return null;
            }

            try
            {

                var startInfo = new ProcessStartInfo(targetFile.FullName)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;

                    var output = new List<string>();

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args?.Data))
                        {
                            output.Add(args.Data);
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();

                    process.WaitForExit((int)TimeSpan.FromMilliseconds(2000).TotalMilliseconds);

                    if (process.ExitCode != 0)
                    {
                        return null;
                    }

                    // Found version string should be like 'NuGet Version: 4.7.1.5393'

                    string foundVersionLine = output.SingleOrDefault(line => line.Trim().StartsWith("NuGet Version:"));

                    string[] semVerParts = foundVersionLine
                        ?.Split(':').LastOrDefault()
                        ?.Trim()
                        ?.Split('.')?
                        .Take(3).ToArray();

                    if (semVerParts is null)
                    {
                        logger.Warning("Could not find current nuget.exe version, could not find expected output");
                        return null;
                    }

                    string mayBeVersion = string.Join(".", semVerParts);

                    if (
                        !SemanticVersion.TryParse(mayBeVersion, out SemanticVersion currentVersion))
                    {
                        logger.Warning("Could not find nuget.exe version from value '{PossibleVersion}'", mayBeVersion);
                        return null;
                    }

                    ImmutableArray<AvailableVersion> availableVersion = await GetAvailableVersionsFromNuGet(
                        httpClient,
                        logger,
                        cancellationToken);

                    if (availableVersion.IsDefaultOrEmpty)
                    {
                        return null;
                    }

                    AvailableVersion newest = availableVersion.OrderByDescending(s => s.SemanticVersion).First();

                    if (newest.SemanticVersion > currentVersion)
                    {
                        logger.Debug(
                            "Newest available version found was {Newest} which is greater than the installed version {Installed}, downloading newer version",
                            newest.SemanticVersion.ToNormalizedString(),
                            currentVersion.ToNormalizedString());

                        NuGetDownloadResult newerResult = await DownloadAsync(logger,
                            newest.DownloadUrl,
                            targetFile,
                            targetFileTempPath,
                            httpClient,
                            cancellationToken);

                        if (newerResult.Succeeded)
                        {
                            return newerResult;
                        }

                        logger.Warning(newerResult.Exception, "Could not download newest nuget.exe version {Version} {Result}", newest.SemanticVersion.ToNormalizedString(), newerResult.Result);
                        return null;
                    }

                    logger.Debug(
                        "Newest available version found was {Newest} which is not greater than the installed version {Installed}",
                        newest.SemanticVersion.ToNormalizedString(),
                        currentVersion.ToNormalizedString());
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not ensure latest version is installed");
            }

            return null;
        }

        private async Task<ImmutableArray<AvailableVersion>> GetAvailableVersionsFromNuGet(
            HttpClient httpClient,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = new Uri("https://dist.nuget.org/index.json");

                string json;
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    using (HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            logger.Warning("Could not get available nuget.exe versions, http status code was not successful {StatusCode}", response.StatusCode);
                            return ImmutableArray<AvailableVersion>.Empty;
                        }

                        json = await response.Content.ReadAsStringAsync();
                    }
                }

                var sample = new
                {
                    artifacts =
                        new[]
                        {
                            new
                            {
                                name = "",
                                displayName = "",
                                versions = new[]
                                {
                                    new
                                    {
                                        displayName = "",
                                        version = "",
                                        url = "",
                                        releasedate = ""
                                    }
                                }
                            }
                        }
                };

                var deserializedAnonymousType = JsonConvert.DeserializeAnonymousType(json, sample);

                ImmutableArray<AvailableVersion>? availableVersions = deserializedAnonymousType.artifacts
                    .SingleOrDefault(artifact =>
                        artifact.name.Equals("win-x86-commandline", StringComparison.OrdinalIgnoreCase))
                    ?.versions
                    .Where(version => version.displayName.Equals("nuget.exe", StringComparison.OrdinalIgnoreCase))
                    .Select(version => new AvailableVersion(new Uri(version.url, UriKind.Absolute),
                        SemanticVersion.Parse(version.version)))
                    .Where(availableVersion => !availableVersion.SemanticVersion.IsPrerelease)
                    .OrderByDescending(availableVersion => availableVersion.SemanticVersion)
                    .ToImmutableArray();

                if (availableVersions?.Length > 0)
                {
                    logger.Debug("Found available NuGet versions [{Count}]\r\n{AvailableVersions}", availableVersions.Value.Length, availableVersions);
                }

                return availableVersions ?? ImmutableArray<AvailableVersion>.Empty;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not get available NuGet versions");
                return ImmutableArray<AvailableVersion>.Empty;
            }
        }
    }
}
