using System;
using System.IO;
using NuGet.Versioning;

namespace Arbor.Tooler;

public record NuGetPackageInstallResult(
        NuGetPackageId NuGetPackageId,
        SemanticVersion? SemanticVersion,
        DirectoryInfo? PackageDirectory)
{
    public static NuGetPackageInstallResult Failed(NuGetPackageId nugetPackageId)
    {
        if (nugetPackageId == null)
        {
            throw new ArgumentNullException(nameof(nugetPackageId));
        }

        return new NuGetPackageInstallResult(nugetPackageId, null, null);
    }

    public override string ToString() =>
        $"{nameof(NuGetPackageId)}: {NuGetPackageId}, {nameof(SemanticVersion)}: {SemanticVersion?.ToNormalizedString()}, {nameof(PackageDirectory)}: {PackageDirectory?.FullName}";
}