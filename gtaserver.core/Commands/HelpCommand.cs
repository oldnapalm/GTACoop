using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Linq;
using GTAServer;

namespace GTAServer.Commands
{
    class HelpCommand : ICommand
    {
        public string CommandName => "help";

        public string HelpText => "Shows a list of commands";

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("Available commands:\n" +
                string.Join(", ", ServerManager.GameServer.Commands.Select(x => (caller.Console) ? x.Key : "/" + x.Key)));
        }
    }
}
