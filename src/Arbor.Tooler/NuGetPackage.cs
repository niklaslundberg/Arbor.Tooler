using System;

namespace Arbor.Tooler
{
    public class NuGetPackage
    {
        public NuGetPackage(NuGetPackageId nugetPackageId, NuGetPackageVersion nugetPackageVersion)
        {
            NuGetPackageId = nugetPackageId ?? throw new ArgumentNullException(nameof(nugetPackageId));
            NuGetPackageVersion = nugetPackageVersion ?? throw new ArgumentNullException(nameof(nugetPackageVersion));
        }

        public NuGetPackageId NuGetPackageId { get; }

        public NuGetPackageVersion NuGetPackageVersion { get; }
    }
}