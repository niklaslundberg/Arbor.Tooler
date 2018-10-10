using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;
using Serilog;
using Serilog.Core;

namespace Arbor.Tooler
{
    public class NuGetPackageInstaller
    {
        private readonly ILogger _logger;
        private readonly NuGetCliSettings _nugetCliSettings;
        private readonly NuGetDownloadClient _nugetDownloadClient;
        private readonly NuGetDownloadSettings _nugetDownloadSettings;

        public NuGetPackageInstaller(
            NuGetDownloadClient nugetDownloadClient = null,
            NuGetCliSettings nugetCliSettings = null,
            NuGetDownloadSettings nugetDownloadSettings = null,
            ILogger logger = null)
        {
            _nugetCliSettings = nugetCliSettings ?? NuGetCliSettings.Default;
            _nugetDownloadClient = nugetDownloadClient ?? new NuGetDownloadClient();
            _nugetDownloadSettings = nugetDownloadSettings ?? NuGetDownloadSettings.Default;
            _logger = logger ?? Logger.None;
        }

        public Task<NuGetPackageInstallResult> InstallPackageAsync(
            string package,
            CancellationToken cancellationToken = default)
        {
            return InstallPackageAsync(
                new NuGetPackage(new NuGetPackageId(package), NuGetPackageVersion.LatestAvailable),
                NugetPackageSettings.Default,
                cancellationToken: cancellationToken);
        }

        public async Task<NuGetPackageInstallResult> InstallPackageAsync(
            NuGetPackage nugetPackage,
            NugetPackageSettings nugetPackageSettings,
            HttpClient httpClient = default,
            DirectoryInfo installBaseDirectory = default,
            CancellationToken cancellationToken = default)
        {
            nugetPackageSettings = nugetPackageSettings ?? NugetPackageSettings.Default;

            _logger.Debug("Using nuget package settings {NuGetPackageSettings}", nugetPackageSettings);

            DirectoryInfo fallbackDirectory = DirectoryHelper.FromPathSegments(
                DirectoryHelper.UserDirectory(),
                "tools",
                "Arbor.Tooler",
                "packages");

            DirectoryInfo packageInstallBaseDirectory = (installBaseDirectory ?? fallbackDirectory).EnsureExists();

            _logger.Debug("Using package install base directory '{PackageBaseDirectory}'",
                packageInstallBaseDirectory.FullName);

            DirectoryInfo packageBaseDir = DirectoryHelper
                .FromPathSegments(
                    packageInstallBaseDirectory.FullName,
                    nugetPackage.NuGetPackageId.PackageId)
                .EnsureExists();

            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[] downloadedPackages =
                GetDownloadedPackages(nugetPackageSettings, packageBaseDir);

            if (nugetPackage.NuGetPackageVersion == NuGetPackageVersion.LatestDownloaded)
            {
                if (downloadedPackages.Length > 0)
                {
                    (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) latest =
                        downloadedPackages.OrderByDescending(version => version.SemanticVersion).First();

                    _logger.Debug("Found existing version {ExistingVersion}",
                        latest.SemanticVersion.ToNormalizedString());

                    return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        latest.SemanticVersion,
                        latest.Directory);
                }

                _logger.Debug("Found no downloaded versions of {Package}", nugetPackage);
            }

            if (nugetPackage.NuGetPackageVersion.SemanticVersion != null)
            {
                (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) downloadedPackage =
                    GetDownloadedPackage(nugetPackage, downloadedPackages);

                if (downloadedPackage != default)
                {
                    _logger.Debug("Found specific version {Package}, version {Version}",
                        nugetPackage.NuGetPackageId.PackageId,
                        nugetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());
                    return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        downloadedPackage.SemanticVersion,
                        downloadedPackage.Directory);
                }
            }

            string nugetExePath = await GetNuGetExePathAsync(httpClient, cancellationToken).ConfigureAwait(false);

            var arguments = new List<string>
            {
                "install",
                nugetPackage.NuGetPackageId.PackageId
            };

            if (nugetPackage.NuGetPackageVersion.SemanticVersion != null)
            {
                arguments.Add("-Version");
                arguments.Add(nugetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());
            }

            if (nugetPackageSettings.AllowPreRelease)
            {
                arguments.Add("-PreRelease");
            }

            using (TempDirectory tempDirectory = TempDirectory.CreateTempDirectory())
            {
                arguments.Add("-OutputDirectory");
                arguments.Add(tempDirectory.Directory.FullName);

                _logger.Debug("Installing package {Package}", nugetPackage);

                string processArgs = string.Join(" ", arguments.Select(argument => $"\"{argument}\""));

                _logger.Debug("Running process {Process} with args {Arguments}", nugetExePath, processArgs);

                var startInfo = new ProcessStartInfo
                {
                    Arguments = processArgs,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    FileName = nugetExePath
                };

                int exitCode;
                using (var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                })
                {
                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                        {
                            _logger.Error("{ProcessMessage}", args.Data);
                        }
                    };

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                        {
                            _logger.Information("{ProcessMessage}", args.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    exitCode = process.ExitCode;
                }

                if (exitCode != 0)
                {
                    _logger.Error("The process {Process} with arguments {Arguments} failed with exit code {ExitCode}",
                        startInfo.FileName,
                        startInfo.Arguments,
                        exitCode);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                string searchPattern = nugetPackage.NuGetPackageId.PackageId + ".*";

                _logger.Debug("Looking for directories in '{Directory}' matching pattern {Pattern}",
                    tempDirectory.Directory.FullName,
                    searchPattern);

                DirectoryInfo packageDirectory =
                    tempDirectory.Directory.GetDirectories(searchPattern)
                        .SingleOrDefault();

                if (packageDirectory is null)
                {
                    _logger.Error(
                        "The expected package package directory '{PackageId}' in temp directory '{FullName}' does not exist",
                        nugetPackage.NuGetPackageId.PackageId,
                        tempDirectory.Directory.FullName);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                string packagePattern = $"{nugetPackage.NuGetPackageId.PackageId}.*.nupkg";

                _logger.Debug("Looking for packages in '{Directory}' matching pattern {Pattern}",
                    tempDirectory.Directory.FullName,
                    packagePattern);

                FileInfo[] nugetPackageFiles =
                    packageDirectory.GetFiles(packagePattern);

                if (nugetPackageFiles.Length == 0)
                {
                    _logger.Error("Could not find expected package {NuGetPackageId} in temp directory '{FullName}'",
                        nugetPackage.NuGetPackageId,
                        packageDirectory.FullName);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                if (nugetPackageFiles.Length > 1)
                {
                    _logger.Error(
                        "Could not find exactly 1 package {NuGetPackageId} in temp directory '{FullName}', found {Length}",
                        nugetPackage.NuGetPackageId,
                        packageDirectory.FullName,
                        nugetPackageFiles.Length);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                FileInfo nugetPackageFile = nugetPackageFiles.Single();

                string nugetPackageFileVersion = Path.GetFileNameWithoutExtension(nugetPackageFile.Name)
                    .Substring(nugetPackage.NuGetPackageId.PackageId.Length + 1);

                if (!SemanticVersion.TryParse(nugetPackageFileVersion,
                    out SemanticVersion nugetPackageFileSemanticVersion))
                {
                    _logger.Error("The downloaded file '{FullName}' is not a semantic version nuget package",
                        nugetPackageFile.FullName);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) existingPackage =
                    GetDownloadedPackage(nugetPackage, downloadedPackages);

                if (existingPackage != default && existingPackage.Directory != null)
                {
                    var nuGetPackageInstallResult = new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        existingPackage.SemanticVersion,
                        existingPackage.Directory);

                    _logger.Debug("Returning existing package result {NuGetPackageInstallResult}",
                        nuGetPackageInstallResult);
                    return nuGetPackageInstallResult;
                }

                DirectoryInfo[] directoryInfos = tempDirectory.Directory.GetDirectories();

                if (directoryInfos.Length != 1)
                {
                    _logger.Error("Expected exactly 1 directory to exist in '{TempDir}' but found {ActualFoundLength}",
                        tempDirectory.Directory.FullName,
                        directoryInfos.Length);

                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                DirectoryInfo workDir = directoryInfos.Single();

                DirectoryInfo targetPackageDirectory = DirectoryHelper.FromPathSegments(packageBaseDir.FullName,
                    nugetPackageFileSemanticVersion.ToNormalizedString());

                targetPackageDirectory.EnsureExists();

                string[] files = workDir.GetFiles("", SearchOption.AllDirectories)
                    .Select(file => file.FullName).ToArray();

                _logger.Debug("Found package files {Files}", files);

                _logger.Debug(
                    "Copying {FileCount} files recursively from '{TempDirectory}' to target '{TargetDirectory}'",
                    workDir.GetFiles("", SearchOption.AllDirectories).Length,
                    workDir.FullName,
                    targetPackageDirectory);

                workDir.CopyRecursiveTo(targetPackageDirectory, _logger);

                return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                    nugetPackageFileSemanticVersion,
                    targetPackageDirectory);
            }
        }

        private static (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) GetDownloadedPackage(
            NuGetPackage nugetPackage,
            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[] downloadedPackages)
        {
            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) downloadedPackage =
                downloadedPackages.SingleOrDefault(package =>
                    package.SemanticVersion == nugetPackage.NuGetPackageVersion.SemanticVersion
                    && package.Directory.EnumerateFiles("*.nupkg", SearchOption.AllDirectories).Any());

            return downloadedPackage;
        }

        private (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[] GetDownloadedPackages(
            NugetPackageSettings nugetPackageSettings,
            DirectoryInfo packageBaseDir)
        {
            IEnumerable<(DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)> versionDirectories =
                packageBaseDir.EnumerateDirectories()
                    .Select(dir =>
                    {
                        bool parsed = SemanticVersion.TryParse(dir.Name, out SemanticVersion semanticVersion);

                        return (Directory: dir, SemanticVersion: semanticVersion, Parsed: parsed);
                    })
                    .Where(tuple => tuple.Parsed
                                    && tuple.Directory.GetFiles("*.nupkg", SearchOption.AllDirectories).Length > 0);

            IEnumerable<(DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)> filtered =
                versionDirectories;

            if (!nugetPackageSettings.AllowPreRelease)
            {
                _logger.Debug("Filtering out pre-release versions in package directory '{PackageDirectory}'",
                    packageBaseDir);
                filtered = filtered.Where(version => !version.SemanticVersion.IsPrerelease);
            }

            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[]
                filteredArray = filtered.ToArray();

            return filteredArray;
        }

        private async Task<string> GetNuGetExePathAsync(
            HttpClient httpClient = default,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_nugetCliSettings.NuGetExePath)
                && File.Exists(_nugetCliSettings.NuGetExePath))
            {
                return _nugetCliSettings.NuGetExePath;
            }

            NuGetDownloadResult nugetDownloadResult =
                await _nugetDownloadClient.DownloadNuGetAsync(_nugetDownloadSettings,
                    _logger,
                    httpClient,
                    cancellationToken).ConfigureAwait(false);

            if (!nugetDownloadResult.Succeeded)
            {
                if (nugetDownloadResult.Exception is null)
                {
                    throw new InvalidOperationException(
                        $"Could not download nuget.exe, {nugetDownloadResult.Result}");
                }

                throw new InvalidOperationException(
                    $"Could not download nuget.exe, {nugetDownloadResult.Result}",
                    nugetDownloadResult.Exception);
            }

            return nugetDownloadResult.NuGetExePath;
        }
    }
}
