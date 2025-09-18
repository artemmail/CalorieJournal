using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FoodBot.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly Func<string, LogLevel, bool> _filter;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _sync = new();

    public FileLoggerProvider(string directory, Func<string, LogLevel, bool>? filter = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory path must be provided", nameof(directory));
        }

        _directory = directory;
        Directory.CreateDirectory(_directory);
        _filter = filter ?? ((_, logLevel) => logLevel >= LogLevel.Information);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal bool IsEnabled(string categoryName, LogLevel logLevel) => _filter(categoryName, logLevel);

    internal void WriteMessage(string message)
    {
        var filePath = Path.Combine(_directory, $"{DateTime.UtcNow:yyyyMMdd}.log");

        lock (_sync)
        {
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.WriteLine(message);
        }
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly IDisposable NoopDisposable = new NullScope();
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string categoryName, FileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable;

        public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(_categoryName, logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter is null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var sb = new StringBuilder();
            sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
            sb.Append(" [");
            sb.Append(logLevel);
            sb.Append("] ");
            sb.Append(_categoryName);
            sb.Append(':');
            sb.Append(' ');
            sb.Append(formatter(state, exception));

            if (exception is not null)
            {
                sb.AppendLine();
                sb.Append(exception);
            }

            _provider.WriteMessage(sb.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
