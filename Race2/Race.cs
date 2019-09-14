using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Race.Commands;
using Race.Objects;

namespace Race
{
    public class Race : IGamemode
    {
        // gamemode information
        public string GamemodeName => "Race";
        public string Name => "Race gamemode";
        public string Description
            => "The orginal race gamemode rewritten for new GTAServer.core";
        public string Author => "TheIndra";

        public static GameServer GameServer;
        public static Session Session;

        public static List<Map> Maps;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            GameServer.RegisterCommands<RaceCommands>();

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            return true;
        }

        public void OnJoin(Client client)
        { }

        private void OnLeave(Client obj)
        { }
    }
}
