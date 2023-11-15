using System.Threading.Tasks;

namespace Arbor.Tooler.ConsoleClient;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var toolerConsole = ToolerConsole.Create(args);
        return await toolerConsole.RunAsync().ConfigureAwait(false);
    }
}