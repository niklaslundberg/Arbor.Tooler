namespace Arbor.Tooler
{
    public class NuGetCliSettings
    {
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
    }
}