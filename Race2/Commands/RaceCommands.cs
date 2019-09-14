using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.ProtocolMessages;
using GTAServer.PluginAPI.Attributes;
using Race.Objects;

namespace Race.Commands
{
    class RaceCommands
    {
        [Command("vote")]
        public static void Vote(Client client, List<string> args)
        {
            if (Race.Session.State != State.Voting) return;
            if (!args.Any())
            {
                client.SendMessage("Use /vote (map), Maps: " + string.Join(", ", Race.Maps.Select(x => x.Name)));
                return;
            }

            if (Race.Session.Votes.ContainsKey(client))
            {
                client.SendMessage("You already voted for this round");
                return;
            }

            if (Race.Maps.All(x => x.Name != args.First()))
            {
                client.SendMessage("No map with that name exists");
                return;
            }

            Race.Session.Votes.Add(client, args.First());
            Race.GameServer.SendChatMessageToAll($"{client.DisplayName} voted for {args.First()}");
        }
    }
}
