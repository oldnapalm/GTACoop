using System;
using GTAServer;
using GTAServer.PluginAPI;
using Microsoft.Extensions.Logging;

namespace Freeroam
{
    public class Freeroam : IGamemode
    {
        public string GamemodeName => "Freeroam";
        public string Name => "Free roam";
        public string Description => "The default implemented Freeroam game mode containing all basics and some commands";
        public string Author => "TheIndra";

        public static GameServer GameServer;
        private static ILogger _logger;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;
            _logger = Util.LoggerFactory.CreateLogger<Freeroam>();

            _logger.LogInformation("Starting Freeroam gamemode");

            return true;
        }
    }
}
