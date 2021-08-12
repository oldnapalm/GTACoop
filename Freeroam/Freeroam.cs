using System;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using GTAServer.Commands;
using GTAServer.PluginAPI.Entities;
using GTAServer.Users;
using System.Threading.Tasks;
using System.Threading;

namespace Freeroam
{
    public class Freeroam : IGamemode
    {
        public string GamemodeName => "Freeroam";
        public string Name => "Free roam";
        public string Description => "The default implemented Freeroam game mode containing all basics and some commands";
        public string Author => "TheIndra";

        public static GameServer GameServer;

        public static FreeroamConfiguration Configuration;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            try
            {
                Configuration = gameServer.InitConfiguration<FreeroamConfiguration>(typeof(Freeroam));
            }
            catch (Exception e)
            {
                Configuration = new FreeroamConfiguration();
            }
            gameServer.RegisterCommands<FreeroamCommands>();

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
