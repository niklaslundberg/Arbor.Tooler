﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Arbor.Processing;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace Arbor.Tooler;

public record PackageFromResource(NuGetPackage Package, FindPackageByIdResource Resource, SourceCacheContext SourceCacheContext);

public class NuGetPackageInstaller
{
    private const string DefaultPrefixPackageId = "packageid:";
    private readonly ILogger _logger;
    private readonly NuGetCliSettings _nugetCliSettings;
    private readonly NuGetDownloadClient _nugetDownloadClient;
    private readonly NuGetDownloadSettings _nugetDownloadSettings;

    private readonly ConcurrentDictionary<string, string> _sourcePrefixes = new();

    public NuGetPackageInstaller(
        NuGetDownloadClient? nugetDownloadClient = null,
        NuGetCliSettings? nugetCliSettings = null,
        NuGetDownloadSettings? nugetDownloadSettings = null,
        ILogger? logger = null)
    {
        _nugetCliSettings = nugetCliSettings ?? NuGetCliSettings.Default;
        _nugetDownloadClient = nugetDownloadClient ?? new NuGetDownloadClient();
        _nugetDownloadSettings = nugetDownloadSettings ?? NuGetDownloadSettings.Default;
        _logger = logger ?? Logger.None;
    }

    public async Task<SemanticVersion?> GetLatestVersionAsync(
        NuGetPackageId packageId,
        string nugetExePath,
        string? nuGetSource = null,
        bool allowPreRelease = false,
        string prefix = DefaultPrefixPackageId,
        int? timeoutInSeconds = 30,
        string? nugetConfig = null,
        bool? adaptiveEnabled = null)
    {
        var allVersions = await GetAllVersionsAsync(
            packageId,
            nugetExePath,
            nuGetSource,
            allowPreRelease,
            prefix,
            timeoutInSeconds,
            nugetConfig,
            adaptiveEnabled).ConfigureAwait(continueOnCapturedContext: false);

        if (allVersions.Length == 0)
        {
            return null;
        }

        return allVersions.Max();
    }

    public async Task<ImmutableArray<SemanticVersion>> GetAllVersions(
        NuGetPackageId packageId,
        string? nuGetSource = default,
        string? nugetConfig = default,
        bool allowPreRelease = false,
        HttpClient? httpClient = default,
        ILogger? logger = default,
        int maxRows = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        bool clientOwned = httpClient is null;
        httpClient ??= new();

        var allVersions = await GetAllVersionsFromApiInternalAsync(
            packageId,
            nuGetSource,
            nugetConfig,
            httpClient,
            logger ?? Logger.None,
            cancellationToken);

        if (clientOwned)
        {
            httpClient.Dispose();
        }

        return Filter(allVersions, allowPreRelease, maxRows);
    }

    private static ImmutableArray<SemanticVersion> Filter(ImmutableArray<PackageFromResource> packages, bool allowPreRelease, int maxRows)
    {
        var semanticVersions =
            packages.Select(package => package.Package.NuGetPackageVersion.SemanticVersion)
                .NotNull()
                .ToHashSet();

        return (allowPreRelease
                ? semanticVersions
                : semanticVersions.Where(semanticVersion => !semanticVersion.IsPrerelease))
            .OrderByDescending(version => version)
            .Take(maxRows)
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<SemanticVersion>> GetAllVersionsAsync(
        NuGetPackageId packageId,
        string? nugetExePath = null,
        string? nuGetSource = null,
        bool allowPreRelease = false,
        string? prefix = DefaultPrefixPackageId,
        int? timeoutInSeconds = 30,
        string? nugetConfig = null,
        bool? adaptiveEnabled = null,
        bool useCli = false,
        int maxRows = int.MaxValue)
    {
        if (!useCli)
        {
            using var client = new HttpClient();
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds ?? 30));

            var allVersions = await GetAllVersionsFromApiInternalAsync(
                packageId,
                nuGetSource,
                nugetConfig,
                client,
                Logger.None,
                tokenSource.Token);

            return allVersions
                .Select(version => version.Package.NuGetPackageVersion.SemanticVersion)
                .NotNull()
                .ToImmutableArray();
        }

        return await GetAllVersionsInternalAsync(
            packageId,
            nugetExePath,
            nuGetSource,
            allowPreRelease,
            prefix,
            timeoutInSeconds,
            nugetConfig,
            adaptiveEnabled: adaptiveEnabled,
            maxRows: maxRows);
    }

    private static async Task<ImmutableArray<PackageFromResource>> GetAllVersionsFromApiInternalAsync(
        NuGetPackageId packageId,
        string? nuGetSource,
        string? nugetConfig,
        HttpClient httpClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ISettings? settings = null;

        if (!string.IsNullOrWhiteSpace(nugetConfig))
        {
            var fileInfo = new FileInfo(nugetConfig);
            settings = Settings.LoadSpecificSettings(fileInfo.Directory!.FullName, fileInfo.Name);
        }

        string currentDirectory = Directory.GetCurrentDirectory();

        settings ??= Settings.LoadDefaultSettings(
            currentDirectory,
            configFileName: null,
            new XPlatMachineWideSetting());

        var sources = SettingsUtility.GetEnabledSources(settings).ToArray();

        var cache = new SourceCacheContext();

        NuGet.Common.ILogger nugetLogger = GetLogger(logger);

        var packageFromResources = new List<PackageFromResource>();

        foreach (var packageSource in sources)
        {
            if (!string.IsNullOrWhiteSpace(nuGetSource)
                && !packageSource.Name.Equals(nuGetSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SourceRepository repository;
            if (packageSource.IsHttp)
            {
                bool isV3Feed;

                if (packageSource.ProtocolVersion == 3)
                {
                    isV3Feed = true;
                }
                else
                {
                    isV3Feed = await IsV3Feed(packageSource, httpClient, HttpMethod.Head, cancellationToken);
                }

                repository = isV3Feed
                    ? Repository.Factory.GetCoreV3(packageSource.SourceUri.ToString())
                    : Repository.Factory.GetCoreV2(packageSource);
            }
            else
            {
                repository = Repository.Factory.GetCoreV2(packageSource);
            }

            if (sources.Length == 1 && sources[0].Credentials is { })
            {
                repository.PackageSource.Credentials ??= packageSource.Credentials;
            }

            FindPackageByIdResource resource =
                await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

            var versions = (await resource.GetAllVersionsAsync(
                packageId.PackageId,
                cache,
                nugetLogger,
                cancellationToken)).ToArray();

            packageFromResources.AddRange(
                versions
                    .Where(nuGetVersion => SemanticVersion.TryParse(nuGetVersion.OriginalVersion!, out _))
                    .Select(nuGetVersion => SemanticVersion.Parse(nuGetVersion.OriginalVersion!)).Select(item => new PackageFromResource(new NuGetPackage(packageId, new NuGetPackageVersion(item)), resource, cache)));
        }

        return packageFromResources.ToImmutableArray();
    }

    private static async Task<bool> IsV3Feed(PackageSource packageSource, HttpClient httpClient, HttpMethod httpMethod, CancellationToken cancellationToken)
    {
        bool isV3Feed;
        using var request = new HttpRequestMessage(httpMethod, packageSource.SourceUri);

        if (!string.IsNullOrWhiteSpace(packageSource.Credentials?.Username) &&
            !string.IsNullOrWhiteSpace(packageSource.Credentials?.Password))
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
                $"{packageSource.Credentials.Username}:{packageSource.Credentials.Password}");
            string encoded = Convert.ToBase64String(bytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            isV3Feed = response.Content.Headers.ContentType?.MediaType?.Contains(
                "json",
                StringComparison.OrdinalIgnoreCase) ?? false;
        }
        else
        {
            if (httpMethod == HttpMethod.Head)
            {
                isV3Feed = await IsV3Feed(packageSource, httpClient, HttpMethod.Get, cancellationToken);
            }
            else
            {
                isV3Feed = false;
            }
        }

        return isV3Feed;
    }

    private static NuGet.Common.ILogger GetLogger(ILogger logger) =>
        logger == Logger.None ? NullLogger.Instance : new SerilogNuGetAdapter(logger);

    private async Task<ImmutableArray<SemanticVersion>> GetAllVersionsInternalAsync(
        NuGetPackageId packageId,
        string? nugetExePath = null,
        string? nuGetSource = null,
        bool allowPreRelease = false,
        string? prefix = default,
        int? timeoutInSeconds = default,
        string? nugetConfig = null,
        bool firstAttempt = true,
        bool? adaptiveEnabled = null,
        int maxRows = int.MaxValue)
    {
        bool adaptiveEnabledValue = _nugetCliSettings.AdaptivePackagePrefixEnabled &&
                                    (!adaptiveEnabled.HasValue || adaptiveEnabled.Value);

        if (adaptiveEnabledValue)
        {
            prefix ??= GetPrefix(nugetConfig, nuGetSource);
        }

        using var tokenSource = timeoutInSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds.Value))
            : new CancellationTokenSource();

        var nugetArguments = new List<string> { "list", $"{prefix}{packageId}", "-AllVersions" };

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
            _logger.Debug(
                "nuget.exe path is not specified when getting all package versions for {PackageId}",
                packageId.PackageId);

            nugetExePath = await GetNuGetExePathAsync(cancellationToken: tokenSource.Token)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        _logger.Information("Getting available versions of package id {PackageId}", packageId.PackageId);

        var ignoredOutputStatements =
            new List<string> { "Using credentials", "No packages found", "MSBuild auto-detection" };

        var lines = new List<string?>();
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

                    if (adaptiveEnabledValue)
                    {
                        string[] packageLines = lines.Where(
                                line =>
                                    !string.IsNullOrWhiteSpace(line)
                                    && !ignoredOutputStatements.Exists(
                                        ignored => line.Contains(ignored, StringComparison.OrdinalIgnoreCase)))
                            .NotNull()
                            .ToArray();

                        int packageLineCount = packageLines.Length;

                        if (packageLineCount > 5 && packageLines.Any(
                                line =>
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
                (message, category) => _logger.Error("{Category} {Message}", category, message),
                toolAction: (message, category) => _logger.Verbose("{Category} {Message}", category, message),
                verboseAction: (message, category) => _logger.Verbose("{Category} {Message}", category, message),
                debugAction: (message, category) => _logger.Debug("{Category} {Message}", category, message),
                cancellationToken: tokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not get versions for package id {PackageId}", packageId.PackageId);

            if (isExplicitlyCancelledWithPackageIdMismatch
                && adaptiveEnabledValue
                && string.IsNullOrWhiteSpace(prefix)
                && firstAttempt)
            {
                var allVersionsWithPrefix = await GetAllVersionsInternalAsync(
                    packageId,
                    nugetExePath,
                    nuGetSource,
                    allowPreRelease,
                    DefaultPrefixPackageId,
                    timeoutInSeconds,
                    nugetConfig,
                    firstAttempt: false,
                    adaptiveEnabled).ConfigureAwait(continueOnCapturedContext: false);

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
            .Where(line => line != null
                           && !ignoredOutputStatements.Exists(ignored =>
                               line.Contains(ignored, StringComparison.InvariantCultureIgnoreCase)))
            .NotNull()
            .ToList();

        var items = included.Select(
                package =>
                {
                    string[] parts = package.Split(separator: ' ');

                    string currentPackageId = parts[0].Trim();

                    try
                    {
                        string version = parts[^1].Trim();

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
                                "Found package '{Package}', skipping because it does not match requested package '{RequestedPackage}'",
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
            .NotNull()
            .OrderBy(packageVersion => packageVersion.NuGetPackageId.PackageId)
            .ThenByDescending(packageVersion => packageVersion.NuGetPackageVersion.SemanticVersion)
            .ToImmutableArray();

        foreach (NuGetPackage nuGetPackage in items)
        {
            _logger.Debug(
                "Found package {PackageId} {PackageVersion}",
                nuGetPackage.NuGetPackageId.PackageId,
                nuGetPackage.NuGetPackageVersion.SemanticVersion?.ToNormalizedString());
        }

        if (items.Length == 0
            && _nugetCliSettings.AdaptivePackagePrefixEnabled
            && !string.IsNullOrWhiteSpace(prefix)
            && firstAttempt)
        {
            _logger.Debug(
                "Could not find any package versions of {PackageId} when using prefix '{Prefix}', getting all NuGet versions without any prefix in search",
                packageId.PackageId,
                prefix);

            var allVersions = await GetAllVersionsInternalAsync(
                packageId,
                nugetExePath,
                nuGetSource,
                allowPreRelease,
                "",
                timeoutInSeconds,
                nugetConfig,
                firstAttempt: false,
                adaptiveEnabled).ConfigureAwait(continueOnCapturedContext: false);

            if (allVersions.Length > 0)
            {
                _logger.Debug(
                    "Found {Count} versions of {PackageId} when removing prefix",
                    allVersions.Length,
                    packageId.PackageId);

                SetPrefix(nugetConfig, nuGetSource, "");
            }

            return allVersions;
        }

        return items
            .Select(item => item.NuGetPackageVersion.SemanticVersion)
            .NotNull()
            .OrderByDescending(version => version)
            .Take(maxRows)
            .ToImmutableArray();
    }

    private void SetPrefix(string? nugetConfig, string? nuGetSource, string? prefix)
    {
        string key = GetConfigSourceKey(nugetConfig, nuGetSource);
        _sourcePrefixes.TryRemove(key, out string? _);

        _sourcePrefixes.TryAdd(key, prefix ?? "");
    }

    private string GetPrefix(string? nugetConfig, string? nuGetSource)
    {
        string key = GetConfigSourceKey(nugetConfig, nuGetSource);

        return _sourcePrefixes.GetValueOrDefault(key, "");
    }

    private static string GetConfigSourceKey(string? nugetConfig, string? nuGetSource) =>
        $"{nugetConfig}_$$$_{nuGetSource}";

    private static (DirectoryInfo? Directory, SemanticVersion? SemanticVersion, bool Parsed) GetDownloadedPackage(
        NuGetPackage nugetPackage,
        (DirectoryInfo Directory, SemanticVersion? SemanticVersion, bool Parsed)[] downloadedPackages)
    {
        var downloadedPackage =
            downloadedPackages.SingleOrDefault(
                package =>
                    package.SemanticVersion == nugetPackage.NuGetPackageVersion.SemanticVersion
                    && package.Directory.EnumerateFiles("*.nupkg", SearchOption.AllDirectories).Any());

        return downloadedPackage;
    }

    private (DirectoryInfo Directory, SemanticVersion? SemanticVersion, bool Parsed)[] GetDownloadedPackages(
        NugetPackageSettings nugetPackageSettings,
        DirectoryInfo packageBaseDir)
    {
        IEnumerable<(DirectoryInfo Directory, SemanticVersion? SemanticVersion, bool Parsed)> versionDirectories =
            packageBaseDir.EnumerateDirectories()
                .Select(
                    dir =>
                    {
                        bool parsed = SemanticVersion.TryParse(dir.Name, out SemanticVersion? semanticVersion);

                        return (Directory: dir, SemanticVersion: semanticVersion, Parsed: parsed);
                    })
                .Where(
                    tuple => tuple.Parsed
                             && tuple.Directory.GetFiles("*.nupkg", SearchOption.AllDirectories).Length > 0);

        IEnumerable<(DirectoryInfo Directory, SemanticVersion? SemanticVersion, bool Parsed)> filtered =
            versionDirectories.Where(item => item.Parsed);

        if (!nugetPackageSettings.AllowPreRelease)
        {
            _logger.Debug(
                "Filtering out pre-release versions in package directory '{PackageDirectory}'",
                packageBaseDir);

            filtered = filtered.Where(version => !version.SemanticVersion!.IsPrerelease);
        }

        return filtered.ToArray();
    }

    private async Task<string?> GetNuGetExePathAsync(
        HttpClient? httpClient = default,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_nugetCliSettings.NuGetExePath)
            && File.Exists(_nugetCliSettings.NuGetExePath) && !_nugetDownloadSettings.Force)
        {
            return _nugetCliSettings.NuGetExePath;
        }

        NuGetDownloadResult nugetDownloadResult =
            await _nugetDownloadClient.DownloadNuGetAsync(
                _nugetDownloadSettings,
                _logger,
                httpClient,
                cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

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
        NugetPackageSettings? nugetPackageSettings = default,
        HttpClient? httpClient = default,
        DirectoryInfo? installBaseDirectory = default,
        CancellationToken cancellationToken = default)
    {
        nugetPackageSettings ??= NugetPackageSettings.Default;

        _logger.Debug("Using nuget package settings {NuGetPackageSettings}", nugetPackageSettings);

        var downloadPathFromEnvironment = DownloadPathFromEnvironment();

        DirectoryInfo fallbackDirectory = downloadPathFromEnvironment ?? DirectoryHelper.FromPathSegments(
            DirectoryHelper.UserLocalAppDataDirectory(),
            "Arbor.Tooler",
            "packages");

        DirectoryInfo packageInstallBaseDirectory = (installBaseDirectory ?? fallbackDirectory).EnsureExists();

        _logger.Debug(
            "Using package install base directory '{PackageBaseDirectory}'",
            packageInstallBaseDirectory.FullName);

        DirectoryInfo packageBaseDir = DirectoryHelper
            .FromPathSegments(
                packageInstallBaseDirectory.FullName,
                nugetPackage.NuGetPackageId.PackageId)
            .EnsureExists();

        (DirectoryInfo Directory, SemanticVersion? SemanticVersion, bool Parsed)[] downloadedPackages =
            GetDownloadedPackages(nugetPackageSettings, packageBaseDir);

        if (nugetPackage.NuGetPackageVersion == NuGetPackageVersion.LatestDownloaded)
        {
            if (downloadedPackages.Length > 0)
            {
                var latest =
                    downloadedPackages.MaxBy(version => version.SemanticVersion);

                _logger.Debug(
                    "Found existing version {ExistingVersion}",
                    latest.SemanticVersion?.ToNormalizedString());

                return new NuGetPackageInstallResult(
                    nugetPackage.NuGetPackageId,
                    latest.SemanticVersion,
                    latest.Directory);
            }

            _logger.Debug("Found no downloaded versions of {Package}", nugetPackage);
        }

        if (nugetPackage.NuGetPackageVersion.SemanticVersion != null)
        {
            var downloadedPackage =
                GetDownloadedPackage(nugetPackage, downloadedPackages);

            if (downloadedPackage != default)
            {
                _logger.Debug(
                    "Found specific version {Package}, version {Version}",
                    nugetPackage.NuGetPackageId.PackageId,
                    nugetPackage.NuGetPackageVersion.SemanticVersion.ToNormalizedString());

                return new NuGetPackageInstallResult(
                    nugetPackage.NuGetPackageId,
                    downloadedPackage.SemanticVersion,
                    downloadedPackage.Directory);
            }
        }

        using var tempDirectory = TempDirectory.CreateTempDirectory(baseTempDirectory: nugetPackageSettings.TempDirectory);

        if (Environment.OSVersion.Platform == PlatformID.Win32NT && nugetPackageSettings.UseCli)
        {
            var result = await DownloadPackageWithNuGetExe(nugetPackage, nugetPackageSettings, tempDirectory, httpClient, cancellationToken);

            if (result == NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId))
            {
                return result;
            }

        }
        else
        {
            var result = await DownloadPackageWithHttp(nugetPackage, nugetPackageSettings, tempDirectory, httpClient, cancellationToken);

            if (result == NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId))
            {
                return result;
            }

            string fileName = GetDownloadFileName(nugetPackage);
            string sourceFile = Path.Combine(tempDirectory.Directory!.FullName, fileName);
            string targetFile = Path.Combine(packageInstallBaseDirectory.FullName, fileName);
            packageInstallBaseDirectory.Create();
            File.Copy(sourceFile,  targetFile);

            if (packageBaseDir.GetFiles().Length + packageBaseDir.GetDirectories().Length == 0)
            {
                packageBaseDir.Delete();
            }

            if (nugetPackageSettings.Extract)
            {
                await using var packageFileStream = File.OpenRead(targetFile);
                var zipArchive = new ZipArchive(packageFileStream);
                zipArchive.ExtractToDirectory(packageBaseDir.FullName);
            }

            return result;
        }

        string searchPattern = $"{nugetPackage.NuGetPackageId.PackageId}.*";

        _logger.Debug(
            "Looking for directories in '{Directory}' matching pattern {Pattern}",
            tempDirectory.Directory!.FullName,
            searchPattern);

        var packageDirectory =
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

        _logger.Debug(
            "Looking for packages in '{Directory}' matching pattern {Pattern}",
            tempDirectory.Directory.FullName,
            packagePattern);

        FileInfo[] nugetPackageFiles =
            packageDirectory.GetFiles(packagePattern);

        if (nugetPackageFiles.Length == 0)
        {
            _logger.Error(
                "Could not find expected package {NuGetPackageId} in temp directory '{FullName}'",
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
            [(nugetPackage.NuGetPackageId.PackageId.Length + 1)..];

        if (!SemanticVersion.TryParse(
                nugetPackageFileVersion,
                out SemanticVersion? nugetPackageFileSemanticVersion))
        {
            _logger.Error(
                "The downloaded file '{FullName}' is not a semantic version nuget package",
                nugetPackageFile.FullName);

            return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
        }

        var existingPackage =
            GetDownloadedPackage(nugetPackage, downloadedPackages);

        if (existingPackage != default && existingPackage.Directory != null)
        {
            var nuGetPackageInstallResult = new NuGetPackageInstallResult(
                nugetPackage.NuGetPackageId,
                existingPackage.SemanticVersion,
                existingPackage.Directory);

            _logger.Debug(
                "Returning existing package result {NuGetPackageInstallResult}",
                nuGetPackageInstallResult);

            return nuGetPackageInstallResult;
        }

        DirectoryInfo[] directoryInfos = tempDirectory.Directory.GetDirectories()
            .Where(dir => dir.Name.StartsWith($"{nugetPackage.NuGetPackageId.PackageId}."))
            .ToArray();

        if (directoryInfos.Length != 1)
        {
            _logger.Error(
                "Expected exactly 1 directory to exist in '{TempDir}' but found {ActualFoundLength}",
                tempDirectory.Directory.FullName,
                directoryInfos.Length);

            return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
        }

        DirectoryInfo workDir = directoryInfos.Single();

        DirectoryInfo targetPackageDirectory = DirectoryHelper.FromPathSegments(
            packageBaseDir.FullName,
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

    private async Task<NuGetPackageInstallResult> DownloadPackageWithHttp(NuGetPackage nugetPackage, NugetPackageSettings nugetPackageSettings, TempDirectory tempDirectory, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        bool clientOwned = httpClient is null;
        httpClient ??= new();

        var allVersions = await GetAllVersionsFromApiInternalAsync(nugetPackage.NuGetPackageId, nugetPackageSettings.NugetSource,
            nugetPackageSettings.NugetConfigFile,
            httpClient, _logger, cancellationToken);

        var first = allVersions.FirstOrDefault(version =>
             version.Package.NuGetPackageVersion.SemanticVersion == nugetPackage.NuGetPackageVersion.SemanticVersion);

        if (first is null)
        {
            _logger.Error("No package versions found for package id {PackageId}, version {Version}", nugetPackage.NuGetPackageId, nugetPackage.NuGetPackageVersion.SemanticVersion!.ToNormalizedString());
            return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
        }

        string tempFileName = GetDownloadFileName(nugetPackage);
        await using var outStream = File.OpenWrite(Path.Combine(tempDirectory.Directory!.FullName, tempFileName));
        await first.Resource.CopyNupkgToStreamAsync(nugetPackage.NuGetPackageId.PackageId,
            NuGetVersion.Parse(nugetPackage.NuGetPackageVersion.SemanticVersion!.ToNormalizedString()), outStream, first.SourceCacheContext,
            new SerilogNuGetAdapter(_logger), cancellationToken);

        await outStream.FlushAsync(cancellationToken);

        if (clientOwned)
        {
            httpClient.Dispose();
        }

        return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
           nugetPackage.NuGetPackageVersion.SemanticVersion, tempDirectory.Directory);
    }

    private static string GetDownloadFileName(NuGetPackage nugetPackage) => $"{nugetPackage.NuGetPackageId}.nupkg";

    private async Task<NuGetPackageInstallResult> DownloadPackageWithNuGetExe(NuGetPackage nugetPackage, NugetPackageSettings nugetPackageSettings, TempDirectory tempDirectory, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        string? nugetExePath = await GetNuGetExePathAsync(httpClient, cancellationToken)
    .ConfigureAwait(continueOnCapturedContext: false);

        var arguments = new List<string> { "install", nugetPackage.NuGetPackageId.PackageId };

        if (!string.IsNullOrWhiteSpace(nugetPackageSettings.NugetConfigFile))
        {
            if (!File.Exists(nugetPackageSettings.NugetConfigFile))
            {
                _logger.Error(
                    "The specified NuGetConfig file {NuGetConfigFile} does not exist",
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

        arguments.Add("-OutputDirectory");
        arguments.Add(tempDirectory.Directory!.FullName);

        _logger.Debug("Installing package {Package}", nugetPackage);

        string processArgs = string.Join(" ", arguments.Select(argument => $"\"{argument}\""));

        _logger.Debug("Running process {Process} with args {Arguments}", nugetExePath, processArgs);

        var exitCode = await ProcessRunner.ExecuteProcessAsync(
            nugetExePath,
            arguments,
            (message, category) => _logger.Information("{Category} {Message}", category, message),
            (message, category) => _logger.Error("{Category} {Message}", category, message),
            toolAction: (message, category) => _logger.Verbose("{Category} {Message}", category, message),
            verboseAction: (message, category) => _logger.Verbose("{Category} {Message}", category, message),
            debugAction: (message, category) => _logger.Debug("{Category} {Message}", category, message),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        if (!exitCode.IsSuccess)
        {
            _logger.Error(
                "The process {Process} with arguments {Arguments} failed with exit code {ExitCode}",
                nugetExePath,
                arguments,
                exitCode);

            return NuGetPackageInstallResult.Failed(nugetPackage.NuGetPackageId);
        }

        return new NuGetPackageInstallResult(nugetPackage.NuGetPackageId,
            nugetPackage.NuGetPackageVersion.SemanticVersion, tempDirectory.Directory);
    }

    public static DirectoryInfo? DownloadPathFromEnvironment() =>
        Environment.GetEnvironmentVariable("ArborTooler_NuGetInstallPath") is { } path && Directory.Exists(path)
            ? new DirectoryInfo(path)
            : null;
}