using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordBot
{
    static class LoggingExtensions
    {
        public static LogLevel ToLogLevel(this LogSeverity severity)
        {
            return (LogLevel)(int)severity - 5;
        }
    }
}
