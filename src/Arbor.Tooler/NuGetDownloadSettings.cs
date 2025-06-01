using System;

namespace Arbor.Tooler;

public class NuGetDownloadSettings(
    bool? nugetDownloadEnabled = null,
    string? nugetExeVersion = null,
    string? nugetDownloadUriFormat = null,
    string? downloadDirectory = null,
    bool? updateEnabled = null,
    bool force = false)
{
    public const bool DefaultNugetExeDownloadEnabled = true;

    public const string DefaultNuGetExeVersion = "latest";

    public const string DefaultNuGetExeDownloadUriFormat =
        "https://dist.nuget.org/win-x86-commandline/{0}/nuget.exe";

    private static readonly Lazy<NuGetDownloadSettings> DefaultSettings = new(() => new NuGetDownloadSettings());

    public string? NugetDownloadUriFormat { get; } = nugetDownloadUriFormat.WithDefault(DefaultNuGetExeDownloadUriFormat);

    public string? DownloadDirectory { get; } = downloadDirectory;
    public bool Force { get; } = force;

    public bool UpdateEnabled { get; } = updateEnabled ?? false;

    public bool NugetDownloadEnabled { get; } = nugetDownloadEnabled ?? DefaultNugetExeDownloadEnabled;

    public string? NugetExeVersion { get; } = nugetExeVersion.WithDefault(DefaultNuGetExeVersion);

    public static NuGetDownloadSettings Default => DefaultSettings.Value;

    public override string ToString() =>
        $"{nameof(NugetDownloadUriFormat)}: {NugetDownloadUriFormat}, {nameof(DownloadDirectory)}: {DownloadDirectory}, {nameof(NugetDownloadEnabled)}: {NugetDownloadEnabled}, {nameof(NugetExeVersion)}: {NugetExeVersion}";
}