using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.PluginAPI.Attributes;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class AdminCommands
    {
        [Command("tp")]
        public static void Teleport(Client client, List<string> args)
        {
            var target = ServerManager.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                client.SendMessage("Player not found");
                return;
            }

            client.Position = target.Position;
            client.SendMessage("Teleported to " + target.DisplayName);
        }

        [Command(("kick"))]
        public static void Kick(Client client, List<string> args)
        {
            var target = ServerManager.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                client.SendMessage("Player not found");
                return;
            }

            target.Kick("Kicked by " + client.DisplayName, false, client);
        }
    }
}
