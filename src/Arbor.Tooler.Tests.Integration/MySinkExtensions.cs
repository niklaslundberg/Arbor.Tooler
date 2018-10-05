using System;
using Serilog;
using Serilog.Configuration;

namespace Arbor.Tooler.Tests.Integration
{
    public static class MySinkExtensions
    {
        public static LoggerConfiguration MySink(
            this LoggerSinkConfiguration loggerConfiguration,
            Action<string> logAction,
            IFormatProvider formatProvider = null)
        {
            return loggerConfiguration.Sink(new MySink(logAction, formatProvider));
        }
    }
}