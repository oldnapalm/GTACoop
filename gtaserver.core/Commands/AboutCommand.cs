using System;
using System.Runtime.InteropServices;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace gtaserver.core.Commands
{
    class AboutCommand : ICommand
    {
        public string CommandName => throw new NotImplementedException();

        public string HelpText => throw new NotImplementedException();

        public bool Restricted => false;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            string os = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                os = "Linux";
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "Windows";
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                os = "OSX";
            }

            caller.SendMessage($"This server runs GTAServer.core on {os}.\n" +
                "More info about this build see gtacoop.com");
        }

    }
}
