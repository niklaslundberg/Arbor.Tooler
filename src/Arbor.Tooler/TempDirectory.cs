using System;
using System.IO;

namespace Arbor.Tooler
{
    internal sealed class TempDirectory : IDisposable
    {
        private TempDirectory(DirectoryInfo directory) =>
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));

        public DirectoryInfo? Directory { get; private set; }

        public void Dispose()
        {
            if (Directory is null)
            {
                return;
            }

            Directory?.Refresh();

            if (Directory?.Exists == true)
            {
                try
                {
                    Directory.Delete(true);
                }
                catch (UnauthorizedAccessException)
                {
                    // ignore
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            Directory = null;
        }

        public static TempDirectory CreateTempDirectory(string? name = null, DirectoryInfo? baseTempDirectory = null) =>
            new(new DirectoryInfo(Path.Combine(baseTempDirectory?.FullName ?? Path.GetTempPath(),
                $"{name.WithDefault("AT")}-{DateTime.UtcNow.Ticks}")).EnsureExists());
    }
}