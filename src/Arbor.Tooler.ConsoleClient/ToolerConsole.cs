using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Arbor.Tooler.ConsoleClient;

public sealed class ToolerConsole : IDisposable
{
    public const string OutputTemplate = "{Message:l}{NewLine}";
    private readonly string[] _args;

    public ILogger Logger { get; private set; }

    private ToolerConsole(string[]? args, ILogger logger)
    {
        Logger = logger;
        _args = args ?? Array.Empty<string>();
    }

    public void Dispose()
    {
        if (Logger is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Logger = null!;
    }

    private void ShowUsage()
    {
        Logger.Information(
            "Example usage to list package versions: {Executable} {Command} {Argument}={ExampleArgument} {Source}={ExampleSource} {Config}={ExampleConfig} {Take}={TakeExample}",
            "dotnet-arbor-tooler",
            CommandExtensions.List,
            CommandExtensions.PackageId,
            "Arbor.Tooler",
            CommandExtensions.Source,
            "nuget.org",
            CommandExtensions.Config,
            "C:\\nuget.config",
            CommandExtensions.Take,
            "5");
        Logger.Information(
            "Example usage to download nuget.exe: {Command} {Argument}={ExampleArgument} {VersionArgument}={ExampleVersionValue}",
            "dotnet-arbor-tooler",
            CommandExtensions.DownloadDirectory,
            @"C:\Tools\NuGet",
            CommandExtensions.ExeVersion,
            "5.4.0");
        Logger.Information(
            "Example usage, default location: {Command} {Argument}={DefaultArgument}",
            "dotnet-arbor-tooler",
            CommandExtensions.DownloadDirectory,
            "default");
    }

    public static ToolerConsole Create(string[] args, ILogger logger) =>
        new(args, logger);

    public static ToolerConsole Create(string[] args, LogEventLevel minLevel = LogEventLevel.Warning)
    {
        Logger logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: OutputTemplate, standardErrorFromLevel: minLevel)
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
            else if (_args.Any(arg => arg.Equals(CommandExtensions.List, StringComparison.OrdinalIgnoreCase))
                     && _args.GetCommandLineValue(CommandExtensions.PackageId) is { } packageId)
            {
                var nuGetPackageInstaller = new NuGetPackageInstaller();

                int maxRows = int.TryParse(_args.GetCommandLineValue(CommandExtensions.Take), out int take) && take > 0 ? take : int.MaxValue;

                string? source = _args.GetCommandLineValue(CommandExtensions.Source);
                string? config = _args.GetCommandLineValue(CommandExtensions.Config);

                bool allowPreRelease = _args.Any(arg =>
                    arg.Equals(CommandExtensions.AllowPreRelease, StringComparison.OrdinalIgnoreCase));

                var packages = await nuGetPackageInstaller.GetAllVersionsAsync(new NuGetPackageId(packageId), nuGetSource: source, nugetConfig: config, maxRows: maxRows, allowPreRelease: allowPreRelease);

                foreach (var package in packages)
                {
                    Logger.Information("{Version}", package.ToNormalizedString());
                }

                exitCode = 0;
            }
            else if (_args[0].Equals(CommandExtensions.Download, StringComparison.OrdinalIgnoreCase)
                     && _args.GetCommandLineValue(CommandExtensions.PackageId) is { } downloadPackageId
                     && _args.GetCommandLineValue(CommandExtensions.PackageVersion) is { } packageVersion
                     && SemanticVersion.TryParse(packageVersion, out var downloadVersion))
            {
                var downloadDirectory =
                    new DirectoryInfo(_args.GetCommandLineValue(CommandExtensions.DownloadDirectory) ??
                                      Directory.GetCurrentDirectory());

                string? source = _args.GetCommandLineValue(CommandExtensions.Source);
                string? config = _args.GetCommandLineValue(CommandExtensions.Config);
                var nugetPackageSettings = new NugetPackageSettings
                {
                    Extract = _args.HashFlag(CommandExtensions.Extract),
                    NugetSource = source,
                    NugetConfigFile = config
                };

                var nuGetPackageInstaller = new NuGetPackageInstaller();
                var nugetPackage = new NuGetPackage(new NuGetPackageId(downloadPackageId), new NuGetPackageVersion(downloadVersion));

                await nuGetPackageInstaller.InstallPackageAsync(nugetPackage, installBaseDirectory: downloadDirectory, nugetPackageSettings: nugetPackageSettings);
                exitCode = 0;
            }
            else
            {
                string? downloadDirectory = _args.GetCommandLineValue(CommandExtensions.DownloadDirectory);
                string? exeVersion = _args.GetCommandLineValue(CommandExtensions.ExeVersion);

                bool force = _args.HashFlag(CommandExtensions.Force);

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
                        new NuGetDownloadSettings(nugetExeVersion: exeVersion, downloadDirectory: downloadDirectory, force: force),
                        Logger).ConfigureAwait(false);

                    if (nuGetDownloadResult.Succeeded)
                    {
                        exitCode = 0;
                    }
                    else
                    {
                        Logger.Error("Could not download NuGet client, {Result}", nuGetDownloadResult.Result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not download NuGet");
        }
        finally
        {
            Logger.Verbose("Exit code is {ExitCode}", exitCode);
        }

        return exitCode;
    }
}