﻿using System;
using Serilog.Core;
using Serilog.Events;

namespace Arbor.Tooler.Tests.Integration
{
    public class MySink : ILogEventSink
    {
        private readonly IFormatProvider _formatProvider;
        private readonly Action<string> _logAction;

        public MySink(Action<string> logAction, IFormatProvider formatProvider)
        {
            _logAction = logAction;
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            string message = logEvent.RenderMessage(_formatProvider);
            _logAction(DateTimeOffset.Now + " " + message);
        }
    }
}
