using System;
using NuGet.Versioning;

namespace Arbor.Tooler
{
    public sealed class NuGetPackageVersion : IEquatable<NuGetPackageVersion>
    {
        public static readonly NuGetPackageVersion Unavailable = new NuGetPackageVersion("n/a");
        public static readonly NuGetPackageVersion LatestAvailable = new NuGetPackageVersion("latest-available");
        public static readonly NuGetPackageVersion LatestDownloaded = new NuGetPackageVersion("latest-downloaded");

        public NuGetPackageVersion(SemanticVersion semanticVersion)
        {
            SemanticVersion = semanticVersion ?? throw new ArgumentNullException(nameof(semanticVersion));
            Version = SemanticVersion.ToNormalizedString();
        }

        private NuGetPackageVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(nameof(version));
            }

            SemanticVersion = null;
            Version = version;
        }

        public SemanticVersion SemanticVersion { get; }

        public string Version { get; }

        public bool Equals(NuGetPackageVersion other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator ==(NuGetPackageVersion left, NuGetPackageVersion right) => Equals(left, right);

        public static bool operator !=(NuGetPackageVersion left, NuGetPackageVersion right) => !Equals(left, right);

        public static bool TryParse(string version, out NuGetPackageVersion nuGetPackageVersion)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                nuGetPackageVersion = Unavailable;
                return false;
            }

            if (LatestAvailable.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
            {
                nuGetPackageVersion = LatestAvailable;
                return true;
            }

            if (LatestDownloaded.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
            {
                nuGetPackageVersion = LatestDownloaded;
                return true;
            }

            if (!SemanticVersion.TryParse(version, out SemanticVersion semanticVersion))
            {
                nuGetPackageVersion = Unavailable;
                return false;
            }

            nuGetPackageVersion = new NuGetPackageVersion(semanticVersion);
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is NuGetPackageVersion other && Equals(other);
        }

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Version);

        public override string ToString()
        {
            if (SemanticVersion != null)
            {
                return SemanticVersion.ToNormalizedString();
            }

            return $"[{Version}]";
        }
    }
}