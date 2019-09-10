using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.Console;

namespace GTAServer.Console.Modules
{
    class CommandsModule : IModule
    {
        public void OnEnable(ConsoleInstance instance)
        {
            instance.AddCommand("who", args =>
            {
                var clients = ServerManager.GameServer.Clients;

                instance.Log($"There are {clients.Count} clients connected:\n" +
                    string.Join("\n", clients.Select((c, i) => $"{i} {c.DisplayName} {c.Latency}ms")));
            });

            instance.AddCommand("say", args =>
            {
                if (args.Count > 0)
                {
                    ServerManager.GameServer.SendChatMessageToAll(string.Join(" ", args));
                    instance.Log("[Chat] <Server>: " + string.Join(" ", args));
                }
            });

            instance.AddCommand("kick", args =>
            {
                if (!args.Any())
                {
                    instance.Log("Please specify the player you want to kick");

                    return;
                }

                var client = ServerManager.GameServer.Clients.Find(x => x.DisplayName == string.Join(" ", args));
                if (client == null)
                {
                    instance.Log("Player not found");

                    return;
                }

                ServerManager.GameServer.KickPlayer(client, "You have been kicked");
            });
        }

        public string Name => "Commands module";

        public string Description => 
            "Contains default console commands like say, kick, who";
    }
}
