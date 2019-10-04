using System;
using JetBrains.Annotations;
using NuGet.Versioning;

namespace Arbor.Tooler
{
    internal class AvailableVersion
    {
        public AvailableVersion([NotNull] Uri downloadUrl, [NotNull] SemanticVersion semanticVersion)
        {
            DownloadUrl = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));
            SemanticVersion = semanticVersion ?? throw new ArgumentNullException(nameof(semanticVersion));
        }

        public Uri DownloadUrl { get; }

        public SemanticVersion SemanticVersion { get; }

        public override string ToString()
        {
            return
                $"{nameof(DownloadUrl)}: {DownloadUrl}, {nameof(SemanticVersion)}: {SemanticVersion.ToNormalizedString()}";
        }
    }
}
