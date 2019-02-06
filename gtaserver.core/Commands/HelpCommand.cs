using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Linq;
using gtaserver.core.ServerSystem;
using GTAServer;

namespace gtaserver.core.Commands
{
    class HelpCommand : ICommand
    {
        public string CommandName => "help";

        public string HelpText => "Shows a list of commands";

        public bool Restricted => false;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("Available commands:\n" +
                string.Join(", ", SystemPlugin.GameServer.Commands.Where(x => (!caller.Console) ? !x.Value.Restricted : true).
                Select(x => (caller.Console) ? x.Key : "/" + x.Key)));
        }
    }
}
