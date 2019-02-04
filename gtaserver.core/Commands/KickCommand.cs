using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace gtaserver.core.Commands
{
    class KickCommand : ICommand
    {
        public string CommandName => "kick";

        public string HelpText => "Kicks a user from the server";

        public bool Restricted => true;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            var args = chatData.Message.Split(" ").Skip(1);

            if (!args.Any())
            {
                caller.SendMessage("Please specify a player you want to kick.");

                return;
            }

            var client = ServerManager.GameServerInstance.Clients.Where(x => x.DisplayName == string.Join(" ", args));
            if (!client.Any())
            {
                caller.SendMessage("Player not found.");

                return;
            }

            ServerManager.GameServerInstance.KickPlayer(client.First(), "You have been kicked", false, caller);
        }
    }
}
