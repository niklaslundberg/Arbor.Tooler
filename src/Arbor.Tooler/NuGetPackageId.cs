using System;

namespace Arbor.Tooler
{
    public class NuGetPackageId : IEquatable<NuGetPackageId>
    {
        public bool Equals(NuGetPackageId? other)
        {
            if (other is null)
            {
                return false;
            }

            return ReferenceEquals(this, other) || string.Equals(PackageId, other.PackageId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as NuGetPackageId);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(PackageId);

        public static bool operator ==(NuGetPackageId? left, NuGetPackageId? right) => Equals(left, right);

        public static bool operator !=(NuGetPackageId? left, NuGetPackageId? right) => !Equals(left, right);

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