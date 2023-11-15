using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arbor.Tooler.Tests.Integration;

internal class LogStringWriter : StringWriter
{
    private readonly List<string> _buffer = new();
    private readonly Action<string?>? _logAction;

    public LogStringWriter(Action<string?>? logAction) => _logAction = logAction;

    private void DoFlush()
    {
        if (_buffer.Count > 0)
        {
            _logAction?.Invoke(string.Concat(_buffer));
        }
    }

    public override void WriteLine(string? value) => _logAction?.Invoke(value);

    public override void Write(string? format, params object?[] arg)
    {
        if (format is null)
        {
            return;
        }

        _logAction?.Invoke(string.Format(format, arg));
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.Contains(Environment.NewLine))
        {
            _logAction?.Invoke(string.Concat(_buffer) + value);
        }
        else
        {
            _buffer.Add(value);
        }
    }

    public override void Flush()
    {
        DoFlush();

        base.Flush();
    }

    public override async Task FlushAsync()
    {
        DoFlush();
        await base.FlushAsync().ConfigureAwait(false);
    }
}