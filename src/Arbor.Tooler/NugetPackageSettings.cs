using System;
using System.IO;

namespace Arbor.Tooler
{
    public class NugetPackageSettings
    {
        private static readonly Lazy<NugetPackageSettings> DefaultSettings =
            new Lazy<NugetPackageSettings>(() => new NugetPackageSettings(false));

        public NugetPackageSettings(bool allowPreRelease, string? nugetSource = null, string? nugetConfigFile = null, DirectoryInfo? tempDirectory = null)
        {
            AllowPreRelease = allowPreRelease;
            NugetSource = nugetSource;
            NugetConfigFile = nugetConfigFile;
            TempDirectory = tempDirectory;
        }

        public bool AllowPreRelease { get; }

        public string? NugetSource { get; }

        public string? NugetConfigFile { get; }

        public DirectoryInfo? TempDirectory { get; }

        public static NugetPackageSettings Default => DefaultSettings.Value;

        public override string ToString() => $"{nameof(AllowPreRelease)}: {AllowPreRelease}";
    }
}