using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;

namespace GTAServer.Commands
{
    class InfoCommands
    {
        [Command("tps")]
        public static void TicksPerSecond(CommandContext ctx, List<string> args)
        {
            ctx.SendMessage("TPS: " + ctx.GameServer.TicksPerSecond);
        }

        [Command("about")]
        public static void About(CommandContext ctx, List<string> args)
        {
            string os = "Unknown";

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
            else if (RuntimeInformation.OSDescription == "web")
            {
                os = "MONO_WASM";
            }

            ctx.SendMessage($"This server runs GTAServer.core {Util.GetServerVersion()} on {os} {RuntimeInformation.OSArchitecture}.\n" +
                               $"More info about this build see gtacoop.com");
        }

        [Command("who")]
        public static void Who(CommandContext ctx, List<string> args)
        {
            var clients = ctx.GameServer.Clients;
            ctx.SendMessage($"Online ({clients.Count}): " + string.Join(", ", clients.Select(x => x.DisplayName)));
        }
    }
}
