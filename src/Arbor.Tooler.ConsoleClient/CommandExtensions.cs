using System;
using System.Collections.Generic;
using System.Linq;

namespace Arbor.Tooler.ConsoleClient
{
    internal static class CommandExtensions
    {
        public const string List = "list";
        public const string PackageId = "-package-id";
        public const string Source = "-source";
        public const string Config = "-config";
        public const string Take = "-take";
        public const string DownloadDirectory = "-outputdirectory";
        public const string ExeVersion = "-exe-version";
        public const string Force = "--force";

        public static string? GetCommandLineValue(this IEnumerable<string> keys, string key)
        {
            string? foundPair = keys.SingleOrDefault(k => k.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase));

            if (foundPair is null)
            {
                return default;
            }

            int separatorIndex = foundPair.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex < 0)
            {
                return default;
            }

            int valueStart = separatorIndex + 1;

            if (foundPair.Length < valueStart)
            {
                return default;
            }

            string commandLineValue = foundPair[valueStart..];

            return commandLineValue;
        }
    }
}