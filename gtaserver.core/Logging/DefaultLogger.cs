using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.Logging
{
    class DefaultLogger : ILogger
    {
        public LogLevel MinimumLevel { get; set; }

        public DefaultLogger(LogLevel minimumLevel)
        {
            MinimumLevel = minimumLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= MinimumLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var logPrefix = logLevel switch
            {
                LogLevel.Information => "[INFO ]",
                LogLevel.Trace =>       "[TRACE]",
                LogLevel.Debug =>       "[DEBUG]",
                LogLevel.Warning =>     "[WARN ]",
                LogLevel.Error =>       "[ERROR]",
                LogLevel.Critical =>    "[CRIT ]",
                _ =>                    "[NONE ]"
            };
            var message = formatter(state, exception);

            Console.ForegroundColor = logLevel switch
            {
                LogLevel.Trace => ConsoleColor.DarkGray,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White,
            };
            Console.WriteLine($"{logPrefix}[{DateTime.Now:hh:mm:ss}][{eventId.Id}/{eventId.Name}] {message}");

            if(exception != null)
            {
                Console.WriteLine(exception);
            }
            Console.ResetColor();
        }
    }
}
