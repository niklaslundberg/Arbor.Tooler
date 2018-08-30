using System;
using System.IO;
using NuGet.Versioning;

namespace Arbor.Tooler
{
    public class NuGetPackageInstallResult
    {
        public NuGetPackageInstallResult(
            NuGetPackageId nugetPackageNuGetPackageId,
            SemanticVersion semanticVersion,
            DirectoryInfo packageDirectory)
        {
            NuGetPackageId = nugetPackageNuGetPackageId ??
                             throw new ArgumentNullException(nameof(nugetPackageNuGetPackageId));
            SemanticVersion = semanticVersion ?? throw new ArgumentNullException(nameof(semanticVersion));
            PackageDirectory = packageDirectory ?? throw new ArgumentNullException(nameof(packageDirectory));
        }

        public NuGetPackageId NuGetPackageId { get; }

        public SemanticVersion SemanticVersion { get; }

        public DirectoryInfo PackageDirectory { get; }
    }
}