using System;

namespace Arbor.Tooler;

public class NuGetCliSettings(
    string? nugetSourceName = null,
    string? nugetConfigFile = null,
    string? nuGetExePath = null,
    bool adaptivePackagePrefixEnabled = true)
{
    private static readonly Lazy<NuGetCliSettings> DefaultSettings = new(() => new NuGetCliSettings());

    public string? NugetSourceName { get; } = nugetSourceName;

    public string? NugetConfigFile { get; } = nugetConfigFile;

    public string? NuGetExePath { get; } = nuGetExePath;

    public bool AdaptivePackagePrefixEnabled { get; } = adaptivePackagePrefixEnabled;

    public static NuGetCliSettings Default => DefaultSettings.Value;

    public override string ToString() =>
        $"{nameof(NugetSourceName)}: {NugetSourceName}, {nameof(NugetConfigFile)}: {NugetConfigFile}, {nameof(NuGetExePath)}: {NuGetExePath}";
}