using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace gtaserver.core.Commands
{
    class PluginsCommand : ICommand
    {
        public string CommandName => "plugins";

        public string HelpText => "Shows all loaded plugins";

        public List<string> RequiredPermissions => null;

        public bool AllPermissionsRequired => false;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("Plugins (" + ServerManager.GetPlugins().Count + "): \n" + 
                String.Join(", ", ServerManager.GetPlugins().Select(x => x.Name)));
        }
    }
}
