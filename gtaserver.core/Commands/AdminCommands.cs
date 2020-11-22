using System;
using System.Collections.Generic;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;

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
    }
}
