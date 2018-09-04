using System;

namespace Arbor.Tooler
{
    public class NugetPackageSettings
    {
        private static readonly Lazy<NugetPackageSettings> _Default = new Lazy<NugetPackageSettings>(() => new NugetPackageSettings(false));

        public NugetPackageSettings(bool allowPreRelease)
        {
            AllowPreRelease = allowPreRelease;
        }

        public bool AllowPreRelease { get; }

        public static NugetPackageSettings Default => _Default.Value;

        public override string ToString()
        {
            return $"{nameof(AllowPreRelease)}: {AllowPreRelease}";
        }
    }
}
