using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.PluginAPI.Attributes;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class UserCommands
    {
        [Command("help")]
        public static void Help(Client client, List<string> args)
        {
            client.SendMessage("Commands: " + string.Join(", ", ServerManager.GameServer.Commands.Select(x => "/" + x.Key)));
        }
    }
}
