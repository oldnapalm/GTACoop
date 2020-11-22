using Microsoft.Extensions.Logging;
using System;

namespace GTAServer.Logging
{
    class ConsoleLogger : ILogger
    {
        public LogLevel MinLevel { get; set; }

        public ConsoleLogger(LogLevel level)
        {
            MinLevel = level;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= MinLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var level = logLevel switch
            {
                LogLevel.Trace =>       new Tuple<ConsoleColor, string>(ConsoleColor.DarkGray, "TRACE"),
                LogLevel.Debug =>       new Tuple<ConsoleColor, string>(ConsoleColor.Gray,     "DEBUG"),
                LogLevel.Information => new Tuple<ConsoleColor, string>(ConsoleColor.White,    " INFO"),
                LogLevel.Warning =>     new Tuple<ConsoleColor, string>(ConsoleColor.Yellow,   " WARN"),
                LogLevel.Error =>       new Tuple<ConsoleColor, string>(ConsoleColor.Red,      "ERROR"),
                LogLevel.Critical =>    new Tuple<ConsoleColor, string>(ConsoleColor.DarkRed,  " CRIT"),
                _ =>                    new Tuple<ConsoleColor, string>(ConsoleColor.White,    "     "),
            };

            Console.ForegroundColor = level.Item1;
            Console.WriteLine($"{DateTime.Now:hh:mm:ss} {level.Item2} [{eventId.Id,2} {eventId.Name}] {message}");

            if (exception != null)
            {
                Console.WriteLine(exception.StackTrace);
            }

            Console.ResetColor();
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
