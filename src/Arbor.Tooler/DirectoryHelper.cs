using System;
using System.IO;

namespace Arbor.Tooler
{
    internal static class DirectoryHelper
    {
        public static string UserDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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

        public static void CopyRecursiveTo(this DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory)
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

            if (!sourceDirectory.Exists)
            {
                return;
            }

            targetDirectory.EnsureExists();

            foreach (FileInfo file in sourceDirectory.EnumerateFiles())
            {
                string targetFilePath = Path.Combine(targetDirectory.FullName, file.Name);

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
