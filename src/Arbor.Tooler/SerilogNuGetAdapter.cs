using System.Threading.Tasks;
using NuGet.Common;
using Serilog.Events;

namespace Arbor.Tooler
{
    internal class SerilogNuGetAdapter : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogNuGetAdapter(Serilog.ILogger logger) => _logger = logger;

        public void LogDebug(string data) => _logger.Debug("{NuGetMessage}", data);

        public void LogVerbose(string data) => _logger.Verbose("{NuGetMessage}", data);

        public void LogInformation(string data) => _logger.Information("{NuGetMessage}", data);

        public void LogMinimal(string data) => _logger.Information("{NuGetMessage}", data);

        public void LogWarning(string data) => _logger.Warning("{NuGetMessage}", data);

        public void LogError(string data) => _logger.Error("{NuGetMessage}", data);

        public void LogInformationSummary(string data) => _logger.Information("{NuGetMessage}", data);

        public void Log(LogLevel level, string data) => _logger.Write(GetLevel(level), "{NuGetMessage}", data);

        public Task LogAsync(LogLevel level, string data)
        {
            _logger.Write(GetLevel(level), "{NuGetMessage}", data);
            return Task.CompletedTask;
        }

        public void Log(ILogMessage message) => _logger.Information("{NuGetMessage}", message.FormatWithCode());

        public Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }

        private LogEventLevel GetLevel(LogLevel level) =>
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
}