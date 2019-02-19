using System;
using System.Collections.Generic;
using System.Linq;

namespace Arbor.Tooler.ConsoleClient
{
    internal static class CommandExtensions
    {
        public static string GetCommandLineValue(this IEnumerable<string> keys, string key)
        {
            var foundPair = keys.SingleOrDefault(k => k.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));

            if (foundPair is null)
            {
                return default;
            }

            int separatorIndex = foundPair.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex < 0)
            {
                return default;
            }

            int valueStart = separatorIndex +1;

            if (foundPair.Length < valueStart)
            {
                return default;
            }

            string  commandLineValue = foundPair.Substring(valueStart);

            return commandLineValue;
        }

        public const string DownloadDirectory = "-outputdirectory";
    }
}
