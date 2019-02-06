using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using gtaserver.core.ServerSystem;

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

            SystemPlugin.GameServer.SendChatMessageToAll(message);
            caller.SendMessage("Send to all: " + message);
        }
    }
}
