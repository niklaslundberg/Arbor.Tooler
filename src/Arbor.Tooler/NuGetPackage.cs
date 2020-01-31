using System;

namespace Arbor.Tooler
{
    public class NuGetPackage
    {
        public NuGetPackage(NuGetPackageId nugetPackageId, NuGetPackageVersion nugetPackageVersion = null)
        {
            NuGetPackageId = nugetPackageId ?? throw new ArgumentNullException(nameof(nugetPackageId));
            NuGetPackageVersion = nugetPackageVersion ?? NuGetPackageVersion.LatestAvailable;
        }

        public NuGetPackageId NuGetPackageId { get; }

        public NuGetPackageVersion NuGetPackageVersion { get; }

        public override string ToString() => $"{NuGetPackageId} {NuGetPackageVersion}";
    }
}