using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace Arbor.Tooler
{
    public class NuGetPackageInstaller
    {
        private readonly NuGetCliSettings _nugetCliSettings;
        private readonly NuGetDownloadClient _nugetDownloadClient;
        private readonly NuGetDownloadSettings _nugetDownloadSettings;

        public NuGetPackageInstaller(
            NuGetCliSettings nugetCliSettings,
            NuGetDownloadClient nugetDownloadClient,
            NuGetDownloadSettings nugetDownloadSettings)
        {
            _nugetCliSettings = nugetCliSettings ?? throw new ArgumentNullException(nameof(nugetCliSettings));
            _nugetDownloadClient = nugetDownloadClient;
            _nugetDownloadSettings = nugetDownloadSettings;
        }

        public async Task<NuGetPackageInstallResult> InstallPackageAsync(
            NugetPackageSettings nugetPackageSettings,
            NuGetPackage nugetPackage,
            DirectoryInfo installBaseDirectory = default,
            CancellationToken cancellationToken = default)
        {
            DirectoryInfo packageInstallBaseDirectory = installBaseDirectory ??
                                                        new DirectoryInfo(Path.Combine(
                                                            Environment.GetFolderPath(Environment.SpecialFolder
                                                                .UserProfile),
                                                            "tools",
                                                            "Arbor.Tooler",
                                                            "packages")).EnsureExists();

            DirectoryInfo packageBaseDir =
                new DirectoryInfo(Path.Combine(packageInstallBaseDirectory.FullName,
                        nugetPackage.NuGetPackageId.PackageId))
                    .EnsureExists();

            (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed)[] downloadedPackages =
                GetDownloadedPackages(nugetPackageSettings, packageBaseDir);

            if (nugetPackage.NuGetPackageVersion == NuGetPackageVersion.LatestDownloaded)
            {
                if (downloadedPackages.Length > 0)
                {
                    (DirectoryInfo Directory, SemanticVersion SemanticVersion, bool Parsed) latest =
                        downloadedPackages.OrderByDescending(version => version.SemanticVersion).First();

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

            DirectoryInfo targetPackageDirectory;

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

                using (var process = new Process
                {
                    StartInfo = startInfo, EnableRaisingEvents = true
                })
                {
                    process.ErrorDataReceived += (sender, args) => { };
                    process.OutputDataReceived += (sender, args) => { };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                }

                DirectoryInfo packageDirectory =
                    tempDirectory.Directory.GetDirectories(nugetPackage.NuGetPackageId.PackageId + ".*")
                        .SingleOrDefault();

                if (packageDirectory is null)
                {
                    throw new InvalidOperationException(
                        $"There is no package directory {nugetPackage.NuGetPackageId.PackageId} in '{tempDirectory.Directory.FullName}'");
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

                targetPackageDirectory = new DirectoryInfo(Path.Combine(packageBaseDir.FullName,
                    nugetPackageFileSemanticVersion.ToNormalizedString()));

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