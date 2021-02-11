using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class InfoCommands
    {
        [Command("tps", Description = "Shows the current ticks per second")]
        public static void TicksPerSecond(CommandContext ctx, List<string> args)
        {
            ctx.SendMessage("TPS: " + ctx.GameServer.TicksPerSecond);
        }

        [Command("about", Description = "Shows information about the current server version and host platform")]
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

        [Command("who", Description = "Shows a list of currently online players")]
        public static void Who(CommandContext ctx, List<string> args)
        {
            var clients = ctx.GameServer.Clients;

            if(ctx.Sender is Client)
            {
                ctx.SendMessage($"Online ({clients.Count}): " + string.Join(", ", clients.Select(x => x.DisplayName)));
            }
            else
            {
                ctx.SendMessage($"Online ({clients.Count}):\n" +
                    string.Join("\n", clients.Select(x => $"\t{x.DisplayName} {x.NetConnection.RemoteEndPoint.Address}" +
                    $" {(int)TimeSpan.FromSeconds(x.Latency).TotalMilliseconds}")) +
                    $"\nYour online players are also shown on gtacoop.com/servers");
            }
        }
    }
}
