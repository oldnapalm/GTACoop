using System.Collections.Generic;
using System.Linq;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class UserCommands
    {
        [Command("help", 
            Description = "Show a list of commands or info about a specific command", Usage = "Usage: help [<command name>]")]
        public static void Help(CommandContext ctx, List<string> args)
        {
            if(args.Count == 0)
            {
                if(ctx.Sender is Client)
                {
                    // minimal version for ingame clients
                    ctx.SendMessage("Commands: " + string.Join(", ", ctx.GameServer.Commands.Select(x => x.Key.Name)));
                }
                else
                {
                    ctx.SendMessage("Commands:\n" +
                        string.Join("\n", ctx.GameServer.Commands.Select(x => $"\t{x.Key.Name}: {x.Key.Description}")) +
                        "\n\n\tUse 'help <command name>' to show more information about a command");
                }

                return;
            }

            var commands = ctx.GameServer.Commands.Where(x => x.Key.Name == args[0]).ToList();
            if(commands.Count == 0)
            {
                ctx.SendMessage("No command found with that name");
                return;
            }

            var command = commands[0].Key;
            ctx.SendMessage($"{command.Name}:\n" +
                $"{command.Usage ?? "Usage: " + command.Name}\n" +
                $"Description: {command.Description}");
        }

        [Command("say", Description = "Send a message to the chat", Usage = "Usage: say <message>")]
        public static void Say(CommandContext ctx, List<string> args)
        {
            if(args.Count == 0)
            {
                ctx.SendMessage("Usage: say <message>");
                return;
            }

            ctx.GameServer.SendChatMessageToAll(ctx.Sender.DisplayName, string.Join(" ", args));
        }
    }
}
