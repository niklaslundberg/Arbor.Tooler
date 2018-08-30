using System;

namespace Arbor.Tooler
{
    public class NuGetCliSettings
    {
        private static readonly Lazy<NuGetCliSettings> _Default = new Lazy<NuGetCliSettings>(() => new NuGetCliSettings());

        public NuGetCliSettings(
            string nugetSourceName = null,
            string nugetConfigFile = null,
            string nuGetExePath = null)
        {
            NugetSourceName = nugetSourceName;
            NugetConfigFile = nugetConfigFile;
            NuGetExePath = nuGetExePath;
        }

        public string NugetSourceName { get; }

        public string NugetConfigFile { get; }

        public string NuGetExePath { get; }
        public static NuGetCliSettings Default => _Default.Value;
    }
}
