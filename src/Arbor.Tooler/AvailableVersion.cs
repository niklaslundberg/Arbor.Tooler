using System;
using NuGet.Versioning;

namespace Arbor.Tooler;

internal class AvailableVersion(Uri downloadUrl, SemanticVersion semanticVersion)
{
    public Uri DownloadUrl { get; } = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));

    public SemanticVersion SemanticVersion { get; } = semanticVersion ?? throw new ArgumentNullException(nameof(semanticVersion));

    public override string ToString() =>
        $"{nameof(DownloadUrl)}: {DownloadUrl}, {nameof(SemanticVersion)}: {SemanticVersion.ToNormalizedString()}";
}