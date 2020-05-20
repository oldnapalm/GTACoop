using System.Collections.Generic;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;

namespace GTAServer.Commands
{
    class UserCommands
    {
        [Command("help")]
        public static void Help(CommandContext ctx, List<string> args)
        {
            ctx.SendMessage("Commands: " + string.Join(", ", ctx.GameServer.Commands.Keys));
        }

        [Command("say")]
        public static void Say(CommandContext ctx, List<string> args)
        {
            ctx.GameServer.SendChatMessageToAll(ctx.Sender.DisplayName, string.Join(" ", args));
        }
    }
}
