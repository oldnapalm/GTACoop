using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class TpCommand : ICommand
    {
        public string HelpText => "Allows you to teleport to another user";

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            var args = chatData.Message.Split().ToList();
            args.RemoveAt(0);

            if (args.Count == 0)
                return;

            var target = ServerManager.GameServer.Clients.Find(x => x.DisplayName == string.Join(" ", args));
            if (target == null)
            {
                caller.SendMessage("Player not found");
                return;
            }

            caller.SendMessage("Teleported to " + target.DisplayName);
            caller.Position = target.Position;
        }
    }
}
