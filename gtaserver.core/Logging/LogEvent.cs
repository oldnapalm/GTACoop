using Microsoft.Extensions.Logging;

namespace GTAServer.Logging
{
    static class LogEvent
    {
        public static EventId Setup { get; } = new EventId(10, "Setup");

        public static EventId Start { get; } = new EventId(11, "Start");

        public static EventId Tick { get; } = new EventId(12, "Tick");

        public static EventId Announce { get; } = new EventId(13, "Announce");

        public static EventId Connection { get; } = new EventId(14, "Connection");

        public static EventId Handshake { get; } = new EventId(15, "Handshake");

        public static EventId StatusChange { get; } = new EventId(16, "StatusChange");

        public static EventId Incoming { get; } = new EventId(17, "Incoming");

        public static EventId Rcon { get; } = new EventId(18, "Rcon");

        public static EventId UsersMgr { get; } = new EventId(19, "User");

        public static EventId PluginLoader { get; } = new EventId(20, "PluginLoader");

        public static EventId Chat { get; } = new EventId(21, "Chat");

        public static EventId UPnP { get; } = new EventId(22, "UPnP");

        public static EventId Plugin { get; } = new EventId(23, "Plugin");
    }
}
