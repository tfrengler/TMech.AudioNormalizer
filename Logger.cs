using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TMech.AudioNormalizer;

public sealed class Logger
{
    private readonly FileInfo _outputFile;
    private readonly SemaphoreSlim _semaphore;
    private readonly LogLevel _minLogLevel;

    public Logger(FileInfo outputFile)
    {
        _semaphore = new SemaphoreSlim(1,1);
        _outputFile = outputFile;
#if DEBUG
        _minLogLevel = LogLevel.DEBUG;
#else
        _minLogLevel = LogLevel.INFO;
#endif
    }

    private async Task Emit(string message, LogLevel level)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length == 0 || level < _minLogLevel)
        {
            return;
        }

        var now = DateTime.Now;
        await File.AppendAllTextAsync(_outputFile.FullName, $"[{now:HH:mm:ss} {level.AsString()}]: {message}\n");
    }

    public async Task Emit(IEnumerable<LogMessage> messages)
    {
        await _semaphore.WaitAsync();
        try
        {
            foreach(var currentMesage in messages)
            {
                await Emit(currentMesage.Message, currentMesage.Level);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Emit(LogMessage message)
    {
        await _semaphore.WaitAsync();
        try
        {
            await Emit(message.Message, message.Level);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public sealed record LogMessage
{
    public string Message { get; } = string.Empty;
    public LogLevel Level { get; }

    public LogMessage(string message, LogLevel level)
    {
        Message = message;
        Level = level;
    }

    public static LogMessage Debug(string message)
    {
        return new LogMessage(message, LogLevel.DEBUG);
    }

    public static LogMessage Info(string message)
    {
        return new LogMessage(message, LogLevel.INFO);
    }

    public static LogMessage Warning(string message)
    {
        return new LogMessage(message, LogLevel.WARNING);
    }

    public static LogMessage Error(string message)
    {
        return new LogMessage(message, LogLevel.ERROR);
    }
}

public enum LogLevel
{
    DEBUG, INFO, WARNING, ERROR
}

public static class LogLevelExtensions
{
    public static string AsString(this LogLevel self)
    {
        return self switch
        {
            LogLevel.DEBUG => "DBG",
            LogLevel.INFO => "NFO",
            LogLevel.WARNING => "WRN",
            LogLevel.ERROR => "ERR",
            _ => throw new InvalidOperationException()
        };
    }
}