using System;
using System.IO;

namespace Arbor.Tooler;

public class NugetPackageSettings
{
    private static readonly Lazy<NugetPackageSettings> DefaultSettings = new();

    public bool AllowPreRelease { get; init; }

    public string? NugetSource { get; init; }

    public string? NugetConfigFile { get; init; }

    public DirectoryInfo? TempDirectory { get; init; }

    public static NugetPackageSettings Default => DefaultSettings.Value;
    public bool UseCli { get; init; }
    public bool Extract { get; init; }

    public override string ToString() => $"{nameof(AllowPreRelease)}: {AllowPreRelease}";
}