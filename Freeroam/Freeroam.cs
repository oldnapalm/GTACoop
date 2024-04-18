using System;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using GTAServer.Commands;

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
            GameEvents.OnChatMessage.Add(OnChatMessage);

            return true;
        }

        private void OnJoin(Client client)
        {
            if (Configuration.SpawnAtSpawn)
            {
                client.Position = Configuration.SpawnCoordinates;
            }
        }

        private PluginResponse<ChatData> OnChatMessage(Client c, ChatData d)
        {
            bool allow = true;

            if (d.Message.ToLower() == "urtle")
            {
                allow = false;
                c.SendNativeCall(0xE3AD2BDBAEE269AC, c.Position.X, c.Position.Y, c.Position.Z, 0, 1000f, true, false, true, false);
                GameServer.SendChatMessageToAll($"{c.DisplayName} said urtle and exploded");
            }

            return new PluginResponse<ChatData>()
            {
                ContinuePluginProc = allow,
                ContinueServerProc = allow,
                Data = d
            };
        }
    }
}
