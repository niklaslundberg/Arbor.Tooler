using System;
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

public sealed class WhenListingPackageVersionsExitCodeShouldBe0OnSuccess : IDisposable
{
    private readonly Logger _logger;

    public WhenListingPackageVersionsExitCodeShouldBe0OnSuccess(ITestOutputHelper testOutputHelper) => _logger = new LoggerConfiguration()
       .WriteTo.TestOutput(testOutputHelper)
       .MinimumLevel.Debug()
       .CreateLogger();

    [Fact]
    public async Task Run()
    {
        string nugetConfigFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath(),
            "tests",
            "Arbor.Tooler.Tests.Integration",
            "DefaultConfig",
            "nuget.config");

        string[] args = { "list", "-package-id=Arbor.Tooler", $"-configFile={nugetConfigFile}" };
        using var toolerConsole = ToolerConsole.Create(args, _logger);
        int exitCode = await toolerConsole.RunAsync();

        exitCode.Should().Be(0);
    }

    public void Dispose() => _logger.Dispose();
}