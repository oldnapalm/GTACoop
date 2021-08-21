using Microsoft.Extensions.Logging;
using System;

namespace GTAServer.Logging
{
    public class ConsoleLoggerProvider : ILoggerProvider
    {
        public LogLevel MinLevel { get; set; }

        public ConsoleLoggerProvider(LogLevel level)
        {
            MinLevel = level;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName, MinLevel);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
