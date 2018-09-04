using System;
using System.IO;
using JetBrains.Annotations;
using NuGet.Versioning;

namespace Arbor.Tooler
{
    public class NuGetPackageInstallResult
    {
        public NuGetPackageInstallResult(
            [NotNull] NuGetPackageId nugetPackageNuGetPackageId,
            [CanBeNull] SemanticVersion semanticVersion,
            [CanBeNull] DirectoryInfo packageDirectory)
        {
            NuGetPackageId = nugetPackageNuGetPackageId ??
                             throw new ArgumentNullException(nameof(nugetPackageNuGetPackageId));
            SemanticVersion = semanticVersion;
            PackageDirectory = packageDirectory;
        }

        public NuGetPackageId NuGetPackageId { get; }

        public SemanticVersion SemanticVersion { get; }

        public DirectoryInfo PackageDirectory { get; }

        public static NuGetPackageInstallResult Failed([NotNull] NuGetPackageId nugetPackageId)
        {
            if (nugetPackageId == null)
            {
                throw new ArgumentNullException(nameof(nugetPackageId));
            }

            return new NuGetPackageInstallResult(nugetPackageId, null, null);
        }

        public override string ToString()
        {
            return $"{nameof(NuGetPackageId)}: {NuGetPackageId}, {nameof(SemanticVersion)}: {SemanticVersion?.ToNormalizedString()}, {nameof(PackageDirectory)}: {PackageDirectory?.FullName}";
        }
    }
}
