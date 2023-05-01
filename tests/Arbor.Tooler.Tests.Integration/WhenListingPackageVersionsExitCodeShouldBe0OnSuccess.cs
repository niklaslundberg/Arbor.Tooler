using System.IO;
using System.Threading.Tasks;
using Arbor.Aesculus.NCrunch;
using Arbor.Tooler.ConsoleClient;
using FluentAssertions;
using Xunit;

namespace Arbor.Tooler.Tests.Integration;

public class WhenListingPackageVersionsExitCodeShouldBe0OnSuccess
{
    [Fact]
    public async Task Run()
    {
        string nugetConfigFile = Path.Combine(VcsTestPathHelper.TryFindVcsRootPath(),
            "tests",
            "Arbor.Tooler.Tests.Integration",
            "DefaultConfig",
            "nuget.config");

        string[] args = { "list", "-package-id=Arbor.Tooler", $"-configFile={nugetConfigFile}" };
        using var toolerConsole = ToolerConsole.Create(args);
        int exitCode = await toolerConsole.RunAsync();

        exitCode.Should().Be(0);
    }
}