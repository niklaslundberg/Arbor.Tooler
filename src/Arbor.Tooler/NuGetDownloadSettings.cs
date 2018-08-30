using System;

namespace Arbor.Tooler
{
    public class NuGetDownloadSettings
    {
        public const bool DefaultNugetExeDownloadEnabled = true;
        public const string DefaultNuGetExeVersion = "latest";

        public const string DefaultNuGetExeDownloadUriFormat =
            "https://dist.nuget.org/win-x86-commandline/{0}/nuget.exe";

        private static readonly Lazy<NuGetDownloadSettings> _Default =
            new Lazy<NuGetDownloadSettings>(() => new NuGetDownloadSettings());

        public NuGetDownloadSettings(
            bool? nugetDownloadEnabled = null,
            string nugetExeVersion = null,
            string nugetDownloadUriFormat = null,
            string downloadDirectory = null)
        {
            NugetDownloadUriFormat = nugetDownloadUriFormat.WithDefault(DefaultNuGetExeDownloadUriFormat);
            DownloadDirectory = downloadDirectory;
            NugetDownloadEnabled = nugetDownloadEnabled ?? DefaultNugetExeDownloadEnabled;
            NugetExeVersion = nugetExeVersion.WithDefault(DefaultNuGetExeVersion);
        }

        public string NugetDownloadUriFormat { get; }

        public string DownloadDirectory { get; }

        public bool NugetDownloadEnabled { get; }

        public string NugetExeVersion { get; }

        public static NuGetDownloadSettings Default => _Default.Value;
    }
}
