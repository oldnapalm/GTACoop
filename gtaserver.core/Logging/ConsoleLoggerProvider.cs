using Microsoft.Extensions.Logging;

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
            return new ConsoleLogger(MinLevel);
        }

        public void Dispose()
        {
        }
    }
}
