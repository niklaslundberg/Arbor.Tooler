using System;

namespace Arbor.Tooler
{
    public class NuGetDownloadSettings
    {
        public const bool DefaultNugetExeDownloadEnabled = true;

        public const string DefaultNuGetExeVersion = "latest";

        public const string DefaultNuGetExeDownloadUriFormat =
            "https://dist.nuget.org/win-x86-commandline/{0}/nuget.exe";

        private static readonly Lazy<NuGetDownloadSettings> DefaultSettings =
            new Lazy<NuGetDownloadSettings>(() => new NuGetDownloadSettings());

        public NuGetDownloadSettings(
            bool? nugetDownloadEnabled = null,
            string? nugetExeVersion = null,
            string? nugetDownloadUriFormat = null,
            string? downloadDirectory = null,
            bool? updateEnabled = null,
            bool force = false)
        {
            NugetDownloadUriFormat = nugetDownloadUriFormat.WithDefault(DefaultNuGetExeDownloadUriFormat);
            DownloadDirectory = downloadDirectory;
            Force = force;
            UpdateEnabled = updateEnabled ?? false;
            NugetDownloadEnabled = nugetDownloadEnabled ?? DefaultNugetExeDownloadEnabled;
            NugetExeVersion = nugetExeVersion.WithDefault(DefaultNuGetExeVersion);
        }

        public string NugetDownloadUriFormat { get; }

        public string DownloadDirectory { get; }
        public bool Force { get; }

        public bool UpdateEnabled { get; }

        public bool NugetDownloadEnabled { get; }

        public string NugetExeVersion { get; }

        public static NuGetDownloadSettings Default => DefaultSettings.Value;

        public override string ToString() =>
            $"{nameof(NugetDownloadUriFormat)}: {NugetDownloadUriFormat}, {nameof(DownloadDirectory)}: {DownloadDirectory}, {nameof(NugetDownloadEnabled)}: {NugetDownloadEnabled}, {nameof(NugetExeVersion)}: {NugetExeVersion}";
    }
}