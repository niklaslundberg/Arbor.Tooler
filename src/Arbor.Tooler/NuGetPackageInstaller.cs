﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Arbor.Processing;
using JetBrains.Annotations;
using NuGet.Versioning;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Arbor.Tooler
{
    public class NuGetPackageInstaller
    {
        private const string DefaultPrefixPackageId = "packageid:";
        private readonly ILogger _logger;
        private readonly NuGetCliSettings _nugetCliSettings;
        private readonly NuGetDownloadClient _nugetDownloadClient;
        private readonly NuGetDownloadSettings _nugetDownloadSettings;

        private readonly ConcurrentDictionary<string, string> _sourcePrefixes =
            new ConcurrentDictionary<string, string>();

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

        [ItemCanBeNull]
        public async Task<SemanticVersion> GetLatestVersionAsync(
            NuGetPackageId packageId,
            string nugetExePath,
            string nuGetSource = null,
            bool allowPreRelease = false,
            string prefix = DefaultPrefixPackageId,
            int? timeoutInSeconds = 30,
            string nugetConfig = null)
        {
            ImmutableArray<SemanticVersion> allVersions = await GetAllVersionsAsync(packageId,
                nugetExePath,
                nuGetSource,
                allowPreRelease,
                prefix,
                timeoutInSeconds,
                nugetConfig);

            if (allVersions.Length == 0)
            {
                return null;
            }

            return allVersions.Max();
        }

        public Task<ImmutableArray<SemanticVersion>> GetAllVersionsAsync(
            NuGetPackageId packageId,
            string nugetExePath = null,
            string nuGetSource = null,
            bool allowPreRelease = false,
            string prefix = DefaultPrefixPackageId,
            int? timeoutInSeconds = 30,
            string nugetConfig = null) =>
            GetAllVersionsInternalAsync(packageId,
                nugetExePath,
                nuGetSource,
                allowPreRelease,
                prefix,
                timeoutInSeconds,
                nugetConfig);

        private async Task<ImmutableArray<SemanticVersion>> GetAllVersionsInternalAsync(
            NuGetPackageId packageId,
            string nugetExePath = null,
            string nuGetSource = null,
            bool allowPreRelease = false,
            string prefix = default,
            int? timeoutInSeconds = default,
            string nugetConfig = null,
            bool firstAttempt = true)
        {
            if (_nugetCliSettings.AdaptivePackagePrefixEnabled)
            {
                if (prefix is null)
                {
                    prefix = GetPrefix(nugetConfig, nuGetSource);
                }
            }

            using var tokenSource = timeoutInSeconds > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds.Value))
                : new CancellationTokenSource();

            var nugetArguments = new List<string> {"list", $"{prefix}{packageId}", "-AllVersions"};

            if (!string.IsNullOrWhiteSpace(nuGetSource))
            {
                nugetArguments.Add("-source");
                nugetArguments.Add(nuGetSource);
            }

            if (!string.IsNullOrWhiteSpace(nugetConfig))
            {
                nugetArguments.Add("-ConfigFile");
                nugetArguments.Add(nugetConfig);
            }

            if (allowPreRelease)
            {
                nugetArguments.Add("-Prerelease");
            }

            if (string.IsNullOrWhiteSpace(nugetExePath))
            {
                _logger.Debug("nuget.exe path is not specified when getting all package versions for {PackageId}",
                    packageId.PackageId);
                nugetExePath = await GetNuGetExePathAsync(cancellationToken: tokenSource.Token);
            }

            _logger.Information("Getting available versions of package id {PackageId}", packageId.PackageId);

            var ignoredOutputStatements =
                new List<string> {"Using credentials", "No packages found", "MSBuild auto-detection"};
            var lines = new List<string>();
            ExitCode exitCode;
            bool isExplicitlyCancelledWithPackageIdMismatch = false;
            try
            {
                exitCode = await ProcessRunner.ExecuteProcessAsync(
                    nugetExePath,
                    nugetArguments,
                    (message, category) =>
                    {
                        _logger.Verbose("{Category} {Message}", category, message);
                        lines.Add(message);

                        var packageLines = lines.Where(line =>
                            !ignoredOutputStatements.Any(ignored =>
                                line.IndexOf(ignored, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();

                        int packageLineCount = packageLines.Length;

                        if (packageLineCount > 5)
                        {
                            if (packageLines.Any(line =>
                                !line.StartsWith(packageId.PackageId, StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.Warning(
                                    "Got packages with other IDs than {PackageId}, aborting package version listing",
                                    packageId.PackageId);
                                isExplicitlyCancelledWithPackageIdMismatch = true;
                                tokenSource.Cancel();
                            }
                        }
                    },
                    (message, category) => { _logger.Error("{Category} {Message}", category, message); },
                    debugAction: (message, category) => { _logger.Debug("{Category} {Message}", category, message); },
                    verboseAction: (message, category) =>
                    {
                        _logger.Verbose("{Category} {Message}", category, message);
                    },
                    toolAction: (message, category) => { _logger.Verbose("{Category} {Message}", category, message); },
                    cancellationToken: tokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not get versions for package id {PackageId}", packageId.PackageId);

                if (isExplicitlyCancelledWithPackageIdMismatch
                    && _nugetCliSettings.AdaptivePackagePrefixEnabled
                    && string.IsNullOrWhiteSpace(prefix)
                    && firstAttempt)
                {
                    ImmutableArray<SemanticVersion> allVersionsWithPrefix = await GetAllVersionsInternalAsync(
                        packageId,
                        nugetExePath,
                        nuGetSource,
                        allowPreRelease,
                        DefaultPrefixPackageId,
                        timeoutInSeconds,
                        nugetConfig,
                        false);

                    if (allVersionsWithPrefix.Length > 0)
                    {
                        SetPrefix(nugetConfig, nuGetSource, DefaultPrefixPackageId);
                        return allVersionsWithPrefix;
                    }
                }

                return ImmutableArray<SemanticVersion>.Empty;
            }

            if (!exitCode.IsSuccess)
            {
                _logger.Warning("Package listing failed with exit code {ExitCode}", exitCode);
                return ImmutableArray<SemanticVersion>.Empty;
            }

            var included = lines
                .Where(line => line != null && !ignoredOutputStatements
                                   .Any(ignored =>
                                       line.IndexOf(ignored, StringComparison.InvariantCultureIgnoreCase) >= 0))
                .ToList();

            var items = included.Select(
                    package =>
                    {
                        var parts = package.Split(' ');

                        string currentPackageId = parts[0];

                        try
                        {
                            string version = parts.Last();

                            if (!SemanticVersion.TryParse(version, out var semanticVersion))
                            {
                                _logger.Verbose(
                                    "Found package version {Version} for package {Package}, skipping because it could not be parsed as semantic version",
                                    version,
                                    currentPackageId);
                                return null;
                            }

                            if (!packageId.PackageId.Equals(currentPackageId, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.Verbose(
                                    "Found package {Package}, skipping because it does match requested package {RequestedPackage}",
                                    currentPackageId,
                                    packageId);

                                return null;
                            }

                            return new NuGetPackage(packageId, new NuGetPackageVersion(semanticVersion));
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Error parsing package '{Package}'", package);
                            return null;
                        }
                    })
                .Where(packageVersion => packageVersion != null)
                .OrderBy(packageVersion => packageVersion.NuGetPackageId.PackageId)
                .ThenByDescending(packageVersion => packageVersion.NuGetPackageVersion.SemanticVersion)
                .ToImmutableArray();

            foreach (NuGetPackage nuGetPackage in items)
            {
                _logger.Debug("Found package {PackageId} {PackageVersion}", nuGetPackage.NuGetPackageId.PackageId,
                    nuGetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());
            }

            if (items.Length == 0
                && _nugetCliSettings.AdaptivePackagePrefixEnabled
                && !string.IsNullOrWhiteSpace(prefix)
                && firstAttempt)
            {
                _logger.Debug(
                    "Could not find any package versions of {PackageId} when using prefix '{Prefix}', getting all NuGet versions without any prefix in search",
                    packageId.PackageId, prefix);

                ImmutableArray<SemanticVersion> allVersions = await GetAllVersionsInternalAsync(
                    packageId,
                    nugetExePath,
                    nuGetSource,
                    allowPreRelease,
                    "",
                    timeoutInSeconds,
                    nugetConfig,
                    false);

                if (allVersions.Length > 0)
                {
                    _logger.Debug("Found {Count} versions of {PackageId} when removing prefix", allVersions.Length,
                        packageId.PackageId);
                    SetPrefix(nugetConfig, nuGetSource, "");
                }

                return allVersions;
            }

            return items.Select(item => item.NuGetPackageVersion.SemanticVersion).ToImmutableArray();
        }

        private void SetPrefix(string nugetConfig, string nuGetSource, string prefix)
        {
            string key = GetConfigSourceKey(nugetConfig, nuGetSource);
            _sourcePrefixes.TryRemove(key, out string _);

            _sourcePrefixes.TryAdd(key, prefix);
        }

        private string GetPrefix(string nugetConfig, string nuGetSource)
        {
            string key = GetConfigSourceKey(nugetConfig, nuGetSource);

            if (!_sourcePrefixes.TryGetValue(key, out string prefix))
            {
                return "";
            }

            return prefix;
        }

        private string GetConfigSourceKey(string nugetConfig, string nuGetSource) => $"{nugetConfig}_$$$_{nuGetSource}";

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

        public Task<NuGetPackageInstallResult> InstallPackageAsync(
            string package,
            CancellationToken cancellationToken = default) =>
            InstallPackageAsync(
                new NuGetPackage(new NuGetPackageId(package), NuGetPackageVersion.LatestAvailable),
                NugetPackageSettings.Default,
                cancellationToken: cancellationToken);

        public async Task<NuGetPackageInstallResult> InstallPackageAsync(
            NuGetPackage nugetPackage,
            NugetPackageSettings nugetPackageSettings,
            HttpClient httpClient = default,
            DirectoryInfo installBaseDirectory = default,
            CancellationToken cancellationToken = default)
        {
            nugetPackageSettings ??= NugetPackageSettings.Default;

            _logger.Debug("Using nuget package settings {NuGetPackageSettings}", nugetPackageSettings);

            DirectoryInfo fallbackDirectory = DirectoryHelper.FromPathSegments(
                DirectoryHelper.UserLocalAppDataDirectory(),
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

                    _logger.Debug(
                        "Found existing version {ExistingVersion}",
                        latest.SemanticVersion.ToNormalizedString());

                    return new NuGetPackageInstallResult(
                        nugetPackage.NuGetPackageId,
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
                    _logger.Debug(
                        "Found specific version {Package}, version {Version}",
                        nugetPackage.NuGetPackageId.PackageId,
                        nugetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());

                    return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
                        downloadedPackage.SemanticVersion,
                        downloadedPackage.Directory);
                }
            }

            string nugetExePath = await GetNuGetExePathAsync(httpClient, cancellationToken).ConfigureAwait(false);

            var arguments = new List<string> {"install", nugetPackage.NuGetPackageId.PackageId};

            if (!string.IsNullOrWhiteSpace(nugetPackageSettings.NugetConfigFile))
            {
                if (!File.Exists(nugetPackageSettings.NugetConfigFile))
                {
                    _logger.Error("The specified NuGetConfig file {NuGetConfigFile} does not exist",
                        nugetPackageSettings.NugetConfigFile);
                    return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
                }

                arguments.Add("-ConfigFile");
                arguments.Add(nugetPackageSettings.NugetConfigFile);
            }

            if (!string.IsNullOrWhiteSpace(nugetPackageSettings.NugetSource))
            {
                arguments.Add("-Source");
                arguments.Add(nugetPackageSettings.NugetSource);
            }

            if (nugetPackage.NuGetPackageVersion.SemanticVersion != null)
            {
                arguments.Add("-Version");
                arguments.Add(nugetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());
            }

            if (nugetPackageSettings.AllowPreRelease)
            {
                arguments.Add("-PreRelease");
            }

            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                arguments.Add("-verbosity");
                arguments.Add("detailed");
            }

            using TempDirectory tempDirectory = TempDirectory.CreateTempDirectory();
            arguments.Add("-OutputDirectory");
            arguments.Add(tempDirectory.Directory.FullName);

            _logger.Debug("Installing package {Package}", nugetPackage);

            string processArgs = string.Join(" ", arguments.Select(argument => $"\"{argument}\""));

            _logger.Debug("Running process {Process} with args {Arguments}", nugetExePath, processArgs);


            ExitCode exitCode = await ProcessRunner.ExecuteProcessAsync(
                nugetExePath,
                arguments,
                (message, category) => { _logger.Information("{Category} {Message}", category, message); },
                (message, category) => { _logger.Error("{Category} {Message}", category, message); },
                debugAction: (message, category) => { _logger.Debug("{Category} {Message}", category, message); },
                verboseAction: (message, category) =>
                {
                    _logger.Verbose("{Category} {Message}", category, message);
                },
                toolAction: (message, category) => { _logger.Verbose("{Category} {Message}", category, message); },
                cancellationToken: cancellationToken);

            if (!exitCode.IsSuccess)
            {
                _logger.Error("The process {Process} with arguments {Arguments} failed with exit code {ExitCode}",
                    nugetExePath,
                    arguments,
                    exitCode);
                return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
            }

            string searchPattern = $"{nugetPackage.NuGetPackageId.PackageId}.*";

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

            DirectoryInfo[] directoryInfos = tempDirectory.Directory.GetDirectories()
                .Where(dir => dir.Name.StartsWith($"{nugetPackage.NuGetPackageId.PackageId}."))
                .ToArray();

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

            string[] files = workDir
                .GetFiles("", SearchOption.AllDirectories)
                .Select(file => file.FullName)
                .ToArray();

            _logger.Debug("Found package files {Files}", files);

            _logger.Debug(
                "Copying {FileCount} files recursively from '{TempDirectory}' to target '{TargetDirectory}'",
                workDir.GetFiles("", SearchOption.AllDirectories).Length,
                workDir.FullName,
                targetPackageDirectory);

            workDir.CopyRecursiveTo(targetPackageDirectory, _logger);

            return new NuGetPackageInstallResult(
                nugetPackage.NuGetPackageId,
                nugetPackageFileSemanticVersion,
                targetPackageDirectory);
        }
    }
}