﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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

        public void Dispose()
        {
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger = null!;
        }

        private void ShowUsage()
        {
            _logger.Information(
                "Example usage: {Command} {Argument}={ExampleArgument} {VersionArgument}={ExampleVersionValue}",
                "dotnet-arbor-tooler",
                CommandExtensions.DownloadDirectory,
                @"C:\Tools\NuGet",
                CommandExtensions.ExeVersion,
                "5.4.0");
            _logger.Information(
                "Example usage, default location: {Command} {Argument}={DefaultArgument}",
                "dotnet-arbor-tooler",
                CommandExtensions.DownloadDirectory,
                "default");
        }

        public static ToolerConsole Create(string[] args)
        {
            Logger logger = new LoggerConfiguration()
                .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Error)
                .MinimumLevel.Debug()
                .CreateLogger();

            return new ToolerConsole(args, logger);
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
                    string? downloadDirectory = _args.GetCommandLineValue(CommandExtensions.DownloadDirectory);
                    string? exeVersion = _args.GetCommandLineValue(CommandExtensions.ExeVersion);

                    bool force = _args.Any(arg =>
                        arg.Equals(CommandExtensions.Force, StringComparison.OrdinalIgnoreCase));

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
                            new NuGetDownloadSettings(downloadDirectory: downloadDirectory, nugetExeVersion: exeVersion, force: force),
                            _logger).ConfigureAwait(false);

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
    }
}