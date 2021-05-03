using System;
using Serilog.Core;
using Serilog.Events;

namespace Arbor.Tooler.Tests.Integration
{
    public class MySink : ILogEventSink
    {
        private readonly IFormatProvider? _formatProvider;
        private readonly Action<string> _logAction;

        public MySink(Action<string> logAction, IFormatProvider? formatProvider)
        {
            _logAction = logAction;
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            string message = logEvent.RenderMessage(_formatProvider);

            if (!string.IsNullOrWhiteSpace(message))
            {
                _logAction($"{DateTimeOffset.Now} [{logEvent.Level}] {message}");
            }
            else
            {
                if (logEvent.Exception != null)
                {
                    _logAction(logEvent.Exception.ToString());
                }
                else
                {
                    _logAction(logEvent.MessageTemplate.Render(logEvent.Properties));
                }
            }
        }
    }
}