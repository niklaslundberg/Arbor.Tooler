using System;

namespace Arbor.Tooler
{
    public class NuGetPackageId
    {
        public NuGetPackageId(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
        }

        public string PackageId { get; }

        public override string ToString() => PackageId;
    }
}