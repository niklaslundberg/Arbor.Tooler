using System;
using System.IO;
using Serilog;

namespace Arbor.Tooler
{
    internal static class DirectoryHelper
    {
        public static string UserLocalAppDataDirectory() =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static DirectoryInfo FromPathSegments(string first, params string[] otherParts)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(first));
            }

            if (otherParts is null || otherParts.Length == 0)
            {
                return new DirectoryInfo(first);
            }

            string fullPath = Path.Combine(first, Path.Combine(otherParts));

            return new DirectoryInfo(fullPath);
        }

        public static DirectoryInfo EnsureExists(this DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null)
            {
                throw new ArgumentNullException(nameof(directoryInfo));
            }

            directoryInfo.Refresh();

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            return directoryInfo;
        }

        public static void CopyRecursiveTo(
            this DirectoryInfo sourceDirectory,
            DirectoryInfo targetDirectory,
            ILogger logger = null)
        {
            if (targetDirectory == null)
            {
                throw new ArgumentNullException(nameof(targetDirectory));
            }

            if (sourceDirectory is null)
            {
                return;
            }

            if (sourceDirectory.FullName.Equals(targetDirectory.FullName))
            {
                throw new InvalidOperationException(
                    $"Could not copy from and to the same directory '{sourceDirectory.FullName}'");
            }

            sourceDirectory.Refresh();

            if (!sourceDirectory.Exists)
            {
                logger?.Verbose("Source directory {Source} does not exist", sourceDirectory.FullName);
                return;
            }

            targetDirectory.EnsureExists();

            foreach (FileInfo file in sourceDirectory.EnumerateFiles())
            {
                string targetFilePath = Path.Combine(targetDirectory.FullName, file.Name);

                logger?.Verbose("Copying file '{From}' '{To}'", file.FullName, targetFilePath);

                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDirectory in sourceDirectory.EnumerateDirectories())
            {
                CopyRecursiveTo(subDirectory,
                    new DirectoryInfo(Path.Combine(targetDirectory.FullName, subDirectory.Name)));
            }
        }
    }
}