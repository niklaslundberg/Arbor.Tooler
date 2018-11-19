using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arbor.Tooler.Tests.Integration
{
    internal class LogStringWriter : StringWriter
    {
        private readonly Action<string> _logAction;

        public LogStringWriter(Action<string> logAction)
        {
            _logAction = logAction;
        }

        public override void WriteLine(string message)
        {
            _logAction?.Invoke(message);
        }

        public override void Write(string message, params object[] args)
        {
            _logAction?.Invoke(string.Format(message, args));
        }

        private List<string> _buffer = new List<string>();

        public override void Write(string message)
        {
            if (message.Contains(Environment.NewLine))
            {
                _logAction?.Invoke(string.Join("", _buffer) +  message);
            }
            else
            {
                _buffer.Add(message);
            }
        }

        public override void Flush()
        {
            DoFlush();

            base.Flush();
        }

        private void DoFlush()
        {
            if (_buffer.Count > 0)
            {
                _logAction?.Invoke(string.Join("", _buffer));
            }
        }

        public override async Task FlushAsync()
        {
            DoFlush();
            await base.FlushAsync();
        }
    }
}
