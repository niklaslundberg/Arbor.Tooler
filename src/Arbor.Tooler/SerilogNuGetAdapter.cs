using System.Threading.Tasks;
using NuGet.Common;
using Serilog.Events;

namespace Arbor.Tooler;

internal class SerilogNuGetAdapter(Serilog.ILogger logger) : ILogger
{
    public void LogDebug(string data) => logger.Debug("{NuGetMessage}", data);

    public void LogVerbose(string data) => logger.Verbose("{NuGetMessage}", data);

    public void LogInformation(string data) => logger.Information("{NuGetMessage}", data);

    public void LogMinimal(string data) => logger.Information("{NuGetMessage}", data);

    public void LogWarning(string data) => logger.Warning("{NuGetMessage}", data);

    public void LogError(string data) => logger.Error("{NuGetMessage}", data);

    public void LogInformationSummary(string data) => logger.Information("{NuGetMessage}", data);

    public void Log(ILogMessage message) => logger.Information("{NuGetMessage}", message.FormatWithCode());

    public void Log(LogLevel level, string data) => logger.Write(GetLevel(level), "{NuGetMessage}", data);

    public Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    public Task LogAsync(LogLevel level, string data)
    {
        logger.Write(GetLevel(level), "{NuGetMessage}", data);
        return Task.CompletedTask;
    }

    private static LogEventLevel GetLevel(LogLevel level) =>
        level switch
        {
            LogLevel.Verbose => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Minimal => LogEventLevel.Information,
            _ => LogEventLevel.Information
        };
}