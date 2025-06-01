using System;

namespace Arbor.Tooler;

public class NuGetPackage(NuGetPackageId nugetPackageId, NuGetPackageVersion? nugetPackageVersion = null)
{
    public NuGetPackageId NuGetPackageId { get; } = nugetPackageId ?? throw new ArgumentNullException(nameof(nugetPackageId));

    public NuGetPackageVersion NuGetPackageVersion { get; } = nugetPackageVersion ?? NuGetPackageVersion.LatestAvailable;

    public override string ToString() => $"{NuGetPackageId} {NuGetPackageVersion}";
}