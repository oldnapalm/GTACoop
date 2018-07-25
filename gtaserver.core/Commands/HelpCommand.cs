using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using GTAServer;

namespace gtaserver.core.Commands
{
    class HelpCommand : ICommand
    {
        public string CommandName => throw new NotImplementedException();

        public string HelpText => throw new NotImplementedException();

        public List<string> RequiredPermissions => throw new NotImplementedException();

        public bool AllPermissionsRequired => throw new NotImplementedException();

            public void OnCommandExec(Client caller, ChatData chatData)
            {
                caller.SendMessage("Available commands:\n" +
                    String.Join(", ", ServerManager.GameServerInstance.Commands.Select(x => "/" + x.Key)));
            }
    }
}
