using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace GTAServer.Logging
{
    class ConsoleLogger : ILogger
    {
        public LogLevel MinLevel { get; set; }

        private string _categoryName;
        private string _parsedCategoryName;

        public ConsoleLogger(string categoryName, LogLevel level)
        {
            _categoryName = categoryName;
            MinLevel = level;

            // taken from old logger
            var r = new Regex(@"\.?.+\.(.+)$");
            var mc = r.Matches(_categoryName);
            _parsedCategoryName = mc[0].Groups[1].Value;
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

            var from = eventId == default ? _parsedCategoryName : eventId.Name;
            Console.WriteLine($"{DateTime.Now:hh:mm:ss} {level.Item2} [{from}] {message}");

            if (exception != null)
            {
                Console.WriteLine(exception.Message);
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
