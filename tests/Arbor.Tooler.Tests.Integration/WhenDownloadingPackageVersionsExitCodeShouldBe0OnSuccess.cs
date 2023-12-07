using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using Arbor.Tooler.ConsoleClient;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;

namespace Arbor.Tooler.Tests.Integration;

public sealed class WhenDownloadingPackageVersionsExitCodeShouldBe0OnSuccess : IDisposable
{
    private readonly Logger _logger;

    public WhenDownloadingPackageVersionsExitCodeShouldBe0OnSuccess(ITestOutputHelper testOutputHelper) =>
        _logger = new LoggerConfiguration()
            .WriteTo.TestOutput(testOutputHelper, outputTemplate: ToolerConsole.OutputTemplate)
            .MinimumLevel.Debug()
            .CreateLogger();

    [Fact]
    public async Task Run()
    {
        using var tempDirectory = TempDirectory.CreateTempDirectory();

        string nugetConfigFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!,
            "tests",
            "Arbor.Tooler.Tests.Integration",
            "DefaultConfig",
            "nuget.config");

        string[] args = { "download", "-package-id=Arbor.Tooler","-version=0.26.0", $"-output-directory={tempDirectory.Directory!.FullName}", $"-configFile={nugetConfigFile}" };
        using var toolerConsole = ToolerConsole.Create(args, _logger);
        int exitCode = await toolerConsole.RunAsync();

        exitCode.Should().Be(0);

        tempDirectory.Directory.GetFiles("*.nupkg").Should().ContainSingle();
    }

    [Fact]
    public async Task RunWithExtract()
    {
        using var tempDirectory = TempDirectory.CreateTempDirectory();

        string nugetConfigFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath()!,
            "tests",
            "Arbor.Tooler.Tests.Integration",
            "DefaultConfig",
            "nuget.config");

        string[] args = { "download", "-package-id=Arbor.Tooler","-version=0.26.0", $"-output-directory={tempDirectory.Directory!.FullName}", $"-configFile={nugetConfigFile}", "--extract" };
        using var toolerConsole = ToolerConsole.Create(args, _logger);
        int exitCode = await toolerConsole.RunAsync();

        exitCode.Should().Be(0);

        tempDirectory.Directory.GetFiles("*.nupkg").Should().ContainSingle();
        var files = tempDirectory.Directory.GetFiles("*", SearchOption.AllDirectories);
        var directories = tempDirectory.Directory.GetDirectories("*", SearchOption.AllDirectories);
        int totalItems = files.Length + directories.Length;

        totalItems.Should().BeGreaterThan(1);
    }

    public void Dispose() => _logger.Dispose();
}