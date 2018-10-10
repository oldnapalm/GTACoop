using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Linq;
using GTAServer;

namespace gtaserver.core.Commands
{
    class HelpCommand : ICommand
    {
        public string CommandName => throw new NotImplementedException();

        public string HelpText => throw new NotImplementedException();

        public bool Restricted => false;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("Available commands:\n" +
                string.Join(", ", ServerManager.GameServerInstance.Commands.Where(x => !x.Value.Restricted).Select(x => "/" + x.Key)));
        }
    }
}
