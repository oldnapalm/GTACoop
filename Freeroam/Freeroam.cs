using System;
using Freeroam.Commands;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
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

        public static FreeroamConfiguration Configuration;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;
            _logger = Util.LoggerFactory.CreateLogger<Freeroam>();

            _logger.LogInformation("Reading configuration");

            try
            {
                Configuration = gameServer.InitConfiguration<FreeroamConfiguration>(typeof(Freeroam));
            }
            catch (Exception e)
            {
                _logger.LogError("Something got wrong while reading configuration of freeroam gamemode, the following exception was thrown: " + e.Message + ". Using default configuration");
                Configuration = new FreeroamConfiguration();
            }
            
            gameServer.Commands.Add("respawn", new RespawnCommand());

            ConnectionEvents.OnJoin.Add(OnJoin);

            return true;
        }

        private void OnJoin(Client client)
        {
            if (Configuration.SpawnAtSpawn)
            {
                client.Position = Configuration.SpawnCoordinates;
            }
        }
    }
}
