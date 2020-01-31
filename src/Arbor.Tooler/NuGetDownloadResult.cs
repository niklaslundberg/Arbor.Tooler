using System;

namespace Arbor.Tooler
{
    public class NuGetDownloadResult
    {
        public static readonly NuGetDownloadResult Disabled = new NuGetDownloadResult(nameof(Disabled));

        public static readonly NuGetDownloadResult MissingNuGetDownloadUriFormat =
            new NuGetDownloadResult(nameof(MissingNuGetDownloadUriFormat));

        public static readonly NuGetDownloadResult MissingDownloadDirectory =
            new NuGetDownloadResult(nameof(MissingDownloadDirectory));

        public static readonly NuGetDownloadResult MissingNuGetExeVersion =
            new NuGetDownloadResult(nameof(MissingNuGetExeVersion));

        private NuGetDownloadResult(string result, bool succeeded = false)
        {
            Result = result;
            Succeeded = succeeded;
            if (succeeded)
            {
                NuGetExePath = result;
            }
        }

        private NuGetDownloadResult(Exception exception) => Exception = exception;

        public string NuGetExePath { get; }

        public string Result { get; }

        public bool Succeeded { get; }

        public Exception Exception { get; }

        public static NuGetDownloadResult FromException(Exception exception) => new NuGetDownloadResult(exception);

        public static NuGetDownloadResult InvalidDownloadUri(string downloadUri) =>
            new NuGetDownloadResult($"Invalid download URI {downloadUri}");

        public static NuGetDownloadResult Success(string targetFilePath) =>
            new NuGetDownloadResult(targetFilePath, true);

        public override string ToString() =>
            $"{nameof(NuGetExePath)}: {NuGetExePath}, {nameof(Result)}: {Result}, {nameof(Succeeded)}: {Succeeded}, {nameof(Exception)}: {Exception}";
    }
}