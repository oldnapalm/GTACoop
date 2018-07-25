using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace gtaserver.core.Commands
{
    class AboutCommand : ICommand
    {
        public string CommandName => throw new NotImplementedException();

        public string HelpText => throw new NotImplementedException();

        public List<string> RequiredPermissions => throw new NotImplementedException();

        public bool AllPermissionsRequired => throw new NotImplementedException();

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("This servers runs GTACooP GTAServer.core\n" +
                "credits:\n" + 
                "- Wolfmitchell 2017\n" +
                "- TheIndra 2018");
        }
    }
}
