using System.Threading.Tasks;
using Arbor.Tooler.ConsoleClient;
using FluentAssertions;
using Xunit;

namespace Arbor.Tooler.Tests.Integration;

public class WhenListingPackageVersionsExitCodeShouldBe0OnSuccess
{
    [Fact]
    public async Task Run()
    {
        string[] args = { "list", "-package-id=Arbor.Tooler" };
        using var toolerConsole = ToolerConsole.Create(args);
        int exitCode = await toolerConsole.RunAsync();

        exitCode.Should().Be(0);
    }
}