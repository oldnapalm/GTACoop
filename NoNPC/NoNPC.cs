using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NoNPC
{
    public class NoNPC : IPlugin
    {
        public string Name => "No NPCs";
        public string Description => "Disable NPC sharing";
        public string Author => "oldnapalm";
        //private GameServer Server;
        private List<string> Forbidden = new();

        private PluginResponse<PedData> OnNpcPed(Client c, PedData p)
        {
            //Server.KickPlayer(c, "NPC sharing is not allowed", true);

            return new PluginResponse<PedData>
            {
                ContinueServerProc = false,
                ContinuePluginProc = false,
                Data = p
            };
        }

        private PluginResponse<VehicleData> OnNpcVehicle(Client c, VehicleData v)
        {
            //Server.KickPlayer(c, "NPC sharing is not allowed", true);

            return new PluginResponse<VehicleData>
            {
                ContinueServerProc = false,
                ContinuePluginProc = false,
                Data = v
            };
        }

        private PluginResponse<ConnectionRequest> ConnectionRequest(Client c, ConnectionRequest r)
        {
            bool allow = !Forbidden.Any(w => r.DisplayName.ToLower().Contains(w));

            return new PluginResponse<ConnectionRequest>
            {
                ContinueServerProc = allow,
                ContinuePluginProc = allow,
                Data = r
            };
        }

        private PluginResponse<ChatData> OnChatMessage(Client c, ChatData d)
        {
            bool allow = !Forbidden.Any(w => d.Message.ToLower().Contains(w));

            return new PluginResponse<ChatData>()
            {
                ContinuePluginProc = allow,
                ContinueServerProc = allow,
                Data = d
            };
        }

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            //Server = gameServer;
            string wordsFile = "Configuration" + Path.DirectorySeparatorChar + "forbidden.txt";
            if (File.Exists(wordsFile))
                Forbidden = File.ReadAllLines(wordsFile).ToList();
            GameEvents.OnNpcPedDataUpdate.Add(OnNpcPed);
            GameEvents.OnNpcVehicleDataUpdate.Add(OnNpcVehicle);
            GameEvents.OnChatMessage.Add(OnChatMessage);
            ConnectionEvents.OnConnectionRequest.Add(ConnectionRequest);
            return true;
        }
    }
}
