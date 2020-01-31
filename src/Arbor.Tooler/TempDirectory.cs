using System;
using System.IO;

namespace Arbor.Tooler
{
    internal sealed class TempDirectory : IDisposable
    {
        private TempDirectory(DirectoryInfo directory) =>
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));

        public DirectoryInfo Directory { get; private set; }

        public void Dispose()
        {
            if (Directory is null)
            {
                return;
            }

            Directory?.Refresh();

            if (Directory.Exists)
            {
                try
                {
                    Directory.Delete(true);
                }
                catch (UnauthorizedAccessException)
                {
                    // ignore
                }
            }

            Directory = null;
        }

        public static TempDirectory CreateTempDirectory(string name = null) =>
            new TempDirectory(new DirectoryInfo(Path.Combine(Path.GetTempPath(),
                $"{name.WithDefault("Arbor.Tooler")}-{DateTime.UtcNow.Ticks}")).EnsureExists());
    }
}