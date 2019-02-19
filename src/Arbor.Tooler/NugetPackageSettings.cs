using System;

namespace Arbor.Tooler
{
    public class NugetPackageSettings
    {
        private static readonly Lazy<NugetPackageSettings> _Default = new Lazy<NugetPackageSettings>(() => new NugetPackageSettings(false));

        public NugetPackageSettings(bool allowPreRelease, string nugetSource = null, string nugetConfigFile = null)
        {
            AllowPreRelease = allowPreRelease;
            NugetSource = nugetSource;
            NugetConfigFile = nugetConfigFile;
        }

        public bool AllowPreRelease { get; }

        public string NugetSource { get; }

        public string NugetConfigFile { get; }

        public static NugetPackageSettings Default => _Default.Value;

        public override string ToString()
        {
            return $"{nameof(AllowPreRelease)}: {AllowPreRelease}";
        }
    }
}
