using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arbor.Tooler.Tests.Integration;

internal class LogStringWriter(Action<string?>? logAction) : StringWriter
{
    private readonly List<string> _buffer = new();

    private void DoFlush()
    {
        if (_buffer.Count > 0)
        {
            logAction?.Invoke(string.Concat(_buffer));
        }
    }

    public override void WriteLine(string? value) => logAction?.Invoke(value);

    public override void Write(string? format, params object?[] arg)
    {
        if (format is null)
        {
            return;
        }

        logAction?.Invoke(string.Format(format, arg));
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.Contains(Environment.NewLine))
        {
            logAction?.Invoke(string.Concat(_buffer) + value);
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