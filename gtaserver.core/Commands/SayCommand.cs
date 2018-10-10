using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace gtaserver.core.Commands
{
    class SayCommand : ICommand
    {
        public string CommandName => "say";

        public string HelpText => "sends a message";

        // the say command is only restricted to the console
        public bool Restricted => true;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            var message = string.Join(" ", chatData.Message.Split(" ").Skip(1));

            ServerManager.GameServerInstance.SendChatMessageToAll(message);
            caller.SendMessage(message);
        }
    }
}
