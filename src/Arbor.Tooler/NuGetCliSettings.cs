using System;

namespace Arbor.Tooler
{
    public class NuGetCliSettings
    {
        private static readonly Lazy<NuGetCliSettings> _Default =
            new Lazy<NuGetCliSettings>(() => new NuGetCliSettings());

        public NuGetCliSettings(
            string nugetSourceName = null,
            string nugetConfigFile = null,
            string nuGetExePath = null,
            bool adaptivePackagePrefixEnabled = true)
        {
            NugetSourceName = nugetSourceName;
            NugetConfigFile = nugetConfigFile;
            NuGetExePath = nuGetExePath;
            AdaptivePackagePrefixEnabled = adaptivePackagePrefixEnabled;
        }

        public string NugetSourceName { get; }

        public string NugetConfigFile { get; }

        public string NuGetExePath { get; }

        public bool AdaptivePackagePrefixEnabled { get; }

        public static NuGetCliSettings Default => _Default.Value;

        public override string ToString() =>
            $"{nameof(NugetSourceName)}: {NugetSourceName}, {nameof(NugetConfigFile)}: {NugetConfigFile}, {nameof(NuGetExePath)}: {NuGetExePath}";
    }
}