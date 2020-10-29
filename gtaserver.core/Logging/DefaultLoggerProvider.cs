using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.Logging
{
    class DefaultLoggerProvider : ILoggerProvider
    {
        public LogLevel MinimumLevel { get; set; }

        public DefaultLoggerProvider(LogLevel minimumLevel = LogLevel.Information)
        {
            MinimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new DefaultLogger(MinimumLevel);
        }

        public void Dispose()
        {
        }
    }
}
