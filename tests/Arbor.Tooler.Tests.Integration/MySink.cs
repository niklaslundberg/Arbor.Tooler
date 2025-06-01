using System;
using Serilog.Core;
using Serilog.Events;

namespace Arbor.Tooler.Tests.Integration;

public class MySink(Action<string> logAction, IFormatProvider? formatProvider) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        string message = logEvent.RenderMessage(formatProvider);

        if (!string.IsNullOrWhiteSpace(message))
        {
            logAction($"{DateTimeOffset.Now} [{logEvent.Level}] {message}");
        }
        else
        {
            if (logEvent.Exception != null)
            {
                logAction(logEvent.Exception.ToString());
            }
            else
            {
                logAction(logEvent.MessageTemplate.Render(logEvent.Properties));
            }
        }
    }
}