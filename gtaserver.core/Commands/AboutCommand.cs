using System;
using System.Runtime.InteropServices;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class AboutCommand : ICommand
    {
        public string CommandName => "about";

        public string HelpText => "Shows information about the current server";

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
                "More info about this build see gtacoop.com.");
        }
    }
}
