using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace Arbor.Tooler.ConsoleClient
{
    internal sealed class ToolerConsole : IDisposable
    {
        private readonly string[] _args;
        private ILogger _logger;

        private ToolerConsole(string[] args, ILogger logger)
        {
            _logger = logger;
            _args = args ?? Array.Empty<string>();
        }

        public static ToolerConsole Create(string[] args)
        {
            Logger logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Debug().CreateLogger();

            return new ToolerConsole(args, logger);
        }

        public void Dispose()
        {
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger = null;
        }

        public async Task<int> RunAsync()
        {
            int exitCode = 1;

            try
            {
                if (_args.Length == 0)
                {
                    ShowUsage();
                }
                else
                {
                    string downloadDirectory = _args.GetCommandLineValue(CommandExtensions.DownloadDirectory);

                    if (string.IsNullOrWhiteSpace(downloadDirectory))
                    {
                        ShowUsage();
                    }
                    else
                    {
                        if (downloadDirectory.Equals("default", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadDirectory = null;
                        }

                        NuGetDownloadResult nuGetDownloadResult = await new NuGetDownloadClient().DownloadNuGetAsync(
                            new NuGetDownloadSettings(downloadDirectory: downloadDirectory),
                            _logger);

                        if (nuGetDownloadResult.Succeeded)
                        {
                            exitCode = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not download NuGet");
            }
            finally
            {
                _logger.Verbose("Exit code is {ExitCode}", exitCode);
            }

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            return exitCode;
        }

        private void ShowUsage()
        {
            _logger.Information(
                "Example usage: {Command} {Argument}{Separator}{ExampleArgument}",
                "dotnet-arbor-tooler",
                CommandExtensions.DownloadDirectory, '=', @"C:\Tools\NuGet");
            _logger.Information(
                "Example usage, default location: {Command} {Argument}{Separator}{DefaultArgument}",
                "dotnet-arbor-tooler",
                CommandExtensions.DownloadDirectory, '=', "default");
        }
    }
}
