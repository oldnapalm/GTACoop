using System.Collections.Generic;
using System.Linq;
using GTAServer.PluginAPI.Attributes;
using Race.Objects;
using GTAServer.PluginAPI.Entities;

namespace Race.Commands
{
    class RaceCommands
    {
        [Command("vote")]
        public static void Vote(CommandContext ctx, List<string> args)
        {
            if (Race.Session.State != State.Voting && Race.Session.State != State.Starting) return;

            if (!args.Any())
            {
                ctx.SendMessage("Use /vote (map), Maps: " + string.Join(", ", Race.Maps.Select(x => x.Name)));
                return;
            }

            if (Race.Session.Votes.ContainsKey(ctx.Client))
            {
                ctx.SendMessage("You already voted for this round");
                return;
            }

            var voted = Race.Maps.FirstOrDefault(x => x.Name.ToLower() == string.Join(" ", args.ToArray()).ToLower());
            if (voted == default)
            {
                ctx.SendMessage("No map with that name exists");
                return;
            }

            Race.Session.Votes.Add(ctx.Client, voted.Name);
            ctx.GameServer.SendChatMessageToAll($"{ctx.Client.DisplayName} voted for {voted.Name}");
        }

        [Command("join")]
        public static void Join(CommandContext ctx, List<string> args)
        {
            if (Race.Session.State != State.Started) return;

            lock (Race.Session.Players)
                if (Race.Session.Players.Any(x => x.Client == ctx.Client))
                {
                    ctx.SendMessage("You are already in the race");
                    return;
                }

            Race.Join(ctx.Client);
            ctx.GameServer.SendChatMessageToAll($"{ctx.Client.DisplayName} joined the race");
        }

        [Command("leave")]
        public static void Leave(CommandContext ctx, List<string> args)
        {
            if (Race.Session.State != State.Started) return;

            Race.Leave(ctx.Client);
            ctx.GameServer.SendChatMessageToAll($"{ctx.Client.DisplayName} left the race");
        }
    }
}
