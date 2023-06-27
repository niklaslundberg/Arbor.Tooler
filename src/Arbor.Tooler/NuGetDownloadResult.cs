using System;
using System.Net;

namespace Arbor.Tooler
{
    public sealed class NuGetDownloadResult
    {
        public static readonly NuGetDownloadResult Disabled = new(nameof(Disabled));

        public static readonly NuGetDownloadResult MissingNuGetDownloadUriFormat =
            new(nameof(MissingNuGetDownloadUriFormat));

        public static readonly NuGetDownloadResult MissingDownloadDirectory = new(nameof(MissingDownloadDirectory));

        public static readonly NuGetDownloadResult MissingNuGetExeVersion = new(nameof(MissingNuGetExeVersion));

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

        public string? NuGetExePath { get; }

        public string? Result { get; }

        public bool Succeeded { get; }

        public Exception? Exception { get; }

        public static NuGetDownloadResult FromException(Exception exception) => new(exception);

        public static NuGetDownloadResult InvalidDownloadUri(string downloadUri) =>
            new($"Invalid download URI {downloadUri}");

        public static NuGetDownloadResult Success(string targetFilePath) => new(targetFilePath, true);

        public override string ToString() =>
            $"{nameof(NuGetExePath)}: {NuGetExePath}, {nameof(Result)}: {Result}, {nameof(Succeeded)}: {Succeeded}, {nameof(Exception)}: {Exception}";

        public static NuGetDownloadResult DownloadFailed(HttpStatusCode statusCode) => new($"Http download status code was {(int)statusCode}", succeeded: false);
        public static NuGetDownloadResult DownloadFailed(string message) => new(message, succeeded: false);
    }
}