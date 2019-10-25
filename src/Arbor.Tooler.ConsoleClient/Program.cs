using System.Threading.Tasks;

namespace Arbor.Tooler.ConsoleClient
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            int exitCode;
            using (ToolerConsole toolerConsole = ToolerConsole.Create(args))
            {
                exitCode = await toolerConsole.RunAsync();
            }

            return exitCode;
        }
    }
}
