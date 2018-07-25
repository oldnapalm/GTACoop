using System;
using System.Collections.Generic;
using System.Text;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace gtaserver.core.Commands
{
    class TpsCommand : ICommand
    {
        public string CommandName => "tps";

        public string HelpText => "Shows the server tick per seconds";

        public List<string> RequiredPermissions => throw new NotImplementedException();

        public bool AllPermissionsRequired => throw new NotImplementedException();

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("TPS: " + ServerManager.GameServerInstance.TicksPerSecond);
        }
    }
}
