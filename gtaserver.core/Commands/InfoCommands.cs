using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GTAServer.PluginAPI.Attributes;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class InfoCommands
    {
        [Command("tps")]
        public static void TicksPerSecond(Client client, List<string> args)
        {
            client.SendMessage("TPS: " + ServerManager.GameServer.TicksPerSecond);
        }

        [Command("plugins")]
        public static void Plugins(Client client, List<string> args)
        {
            client.SendMessage("Plugins (" + ServerManager.GetPlugins().Count + "): \n " +
                               string.Join(", ", ServerManager.GetPlugins().Select(x => x.Name)));
        }

        [Command("about")]
        public static void About(Client client, List<string> args)
        {
            string os = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                os = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                os = "OSX";
            }

            client.SendMessage($"This server runs GTAServer.core on {os} {RuntimeInformation.OSArchitecture}.\n" +
                               $"More info about this build see gtacoop.com");
        }
    }
}
