using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            _nugetDownloadClient = nugetDownloadClient ?? NuGetDownloadClient.CreateDefault();
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
            DirectoryInfo installBaseDirectory = default,
            CancellationToken cancellationToken = default)
        {
            nugetPackageSettings = nugetPackageSettings ?? NugetPackageSettings.Default;

            DirectoryInfo fallbackDirectory = DirectoryHelper.FromPathSegments(
                DirectoryHelper.UserDirectory(),
                "tools",
                "Arbor.Tooler",
                "packages");

            DirectoryInfo packageInstallBaseDirectory = (installBaseDirectory ?? fallbackDirectory).EnsureExists();

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
            }

            if (nugetPackage.NuGetPackageVersion.SemanticVersion != null)
            {
                (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) downloadedPackage =
                    GetDownloadedPackage(nugetPackage, downloadedPackages);

                if (downloadedPackage != default)
                {
                    return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        downloadedPackage.SemanticVersion,
                        downloadedPackage.Directory);
                }
            }

            string nugetExePath = await GetNuGetExePathAsync(cancellationToken);

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

            using (TempDirectory tempDirectory = TempDirectory.CreateTempDirectory())
            {
                arguments.Add("-OutputDirectory");
                arguments.Add(tempDirectory.Directory.FullName);

                var startInfo = new ProcessStartInfo
                {
                    Arguments = string.Join(" ", arguments.Select(argument => $"\"{argument}\"")),
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
                    _logger.Error("The process {Process} with arguments {Arguments} failed with exit code {ExitCode}", startInfo.FileName, startInfo.Arguments, exitCode);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                DirectoryInfo packageDirectory =
                    tempDirectory.Directory.GetDirectories(nugetPackage.NuGetPackageId.PackageId + ".*")
                        .SingleOrDefault();

                if (packageDirectory is null)
                {
                    throw new InvalidOperationException(
                        $"The expected package package directory '{nugetPackage.NuGetPackageId.PackageId}' in temp directory '{tempDirectory.Directory.FullName}' does not exist");
                }

                FileInfo[] nugetPackageFiles =
                    packageDirectory.GetFiles($"{nugetPackage.NuGetPackageId.PackageId}.*.nupkg");

                if (nugetPackageFiles.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Could not find expected package {nugetPackage.NuGetPackageId} in temp directory '{packageDirectory.FullName}'");
                }

                if (nugetPackageFiles.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Could not find exactly 1 package {nugetPackage.NuGetPackageId} in temp directory '{packageDirectory.FullName}', found {nugetPackageFiles.Length}");
                }

                FileInfo nugetPackageFile = nugetPackageFiles.Single();

                string nugetPackageFileVersion = Path.GetFileNameWithoutExtension(nugetPackageFile.Name)
                    .Substring(nugetPackage.NuGetPackageId.PackageId.Length + 1);

                if (!SemanticVersion.TryParse(nugetPackageFileVersion,
                    out SemanticVersion nugetPackageFileSemanticVersion))
                {
                    throw new InvalidOperationException(
                        $"The downloaded file '{nugetPackageFile.FullName}' is not a semantic version nuget package");
                }

                (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) existingPackage =
                    GetDownloadedPackage(nugetPackage, downloadedPackages);

                if (existingPackage != default)
                {
                    return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        existingPackage.SemanticVersion,
                        existingPackage.Directory);
                }

                DirectoryInfo targetPackageDirectory = DirectoryHelper.FromPathSegments(packageBaseDir.FullName,
                    nugetPackageFileSemanticVersion.ToNormalizedString());

                targetPackageDirectory.EnsureExists();

                tempDirectory.Directory.CopyRecursiveTo(targetPackageDirectory);

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
                    package.SemanticVersion == nugetPackage.NuGetPackageVersion.SemanticVersion);
            return downloadedPackage;
        }

        private static (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[] GetDownloadedPackages(
            NugetPackageSettings nugetPackageSettings,
            DirectoryInfo packageBaseDir)
        {
            IEnumerable<(DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)> versionDirectories =
                packageBaseDir.EnumerateDirectories().Select(dir =>
                {
                    bool parsed = SemanticVersion.TryParse(dir.Name, out SemanticVersion semanticVersion);

                    return (Directory: dir, SemanticVersion: semanticVersion, Parsed: parsed);
                }).Where(tuple => tuple.Parsed);

            IEnumerable<(DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)> filtered =
                versionDirectories;

            if (!nugetPackageSettings.AllowPreRelease)
            {
                filtered = filtered.Where(version => !version.SemanticVersion.IsPrerelease);
            }

            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[]
                filteredArray = filtered.ToArray();
            return filteredArray;
        }

        private async Task<string> GetNuGetExePathAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_nugetCliSettings.NuGetExePath)
                && File.Exists(_nugetCliSettings.NuGetExePath))
            {
                return _nugetCliSettings.NuGetExePath;
            }

            NuGetDownloadResult nugetDownloadResult =
                await _nugetDownloadClient.DownloadNuGetAsync(_nugetDownloadSettings, cancellationToken);

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
