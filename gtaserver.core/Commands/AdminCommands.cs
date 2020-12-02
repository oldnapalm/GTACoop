using System;
using System.Collections.Generic;
using System.Diagnostics;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.Commands
{
    class AdminCommands
    {
        [Command("tp")]
        public static void Teleport(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is ConsoleCommandSender)
            {
                ctx.SendMessage("You cannot execute this command as console");
                return;
            }

            var target = ctx.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                ctx.SendMessage("Player not found");
                return;
            }

            ctx.Client.Position = target.Position;
            ctx.SendMessage("Teleported to " + target.DisplayName);
        }

        [Command(("kick"))]
        public static void Kick(CommandContext ctx, List<string> args)
        {
            var target = ctx.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                ctx.SendMessage("Player not found");
                return;
            }

            target.Kick("Kicked by " + ctx.Sender.DisplayName, false);
        }

        [Command("status")]
        public static void Status(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is Client)
            {
                ctx.SendMessage("This command can only be used as console");
                return;
            }

            var uptime = (DateTime.Now - Process.GetCurrentProcess().StartTime);
            var s = ctx.GameServer.Server.Statistics;

            ctx.SendMessage($"{uptime:hh\\:mm\\:ss} up {Math.Round(uptime.TotalDays)} days\n" +
                "Sent: " + NetUtility.ToHumanReadable(s.SentBytes) + "\n" +
                "Received: " + NetUtility.ToHumanReadable(s.ReceivedBytes) + "\n" +
                "Active connections: " + ctx.GameServer.Server.ConnectionsCount + "\n");
        }
    }
}
