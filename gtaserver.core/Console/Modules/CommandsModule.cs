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

                instance.WriteLn($"There are {clients.Count} clients connected:\n" +
                    string.Join("\n", clients.Select((c, i) => $"{i} {c.DisplayName} {c.Latency}ms")));
            });

            instance.AddCommand("tps", args =>
            {
                instance.WriteLn("TPS: " + ServerManager.GameServer.TicksPerSecond);
            });

            instance.AddCommand("say", args =>
            {
                if (args.Count > 0)
                {
                    ServerManager.GameServer.SendChatMessageToAll(string.Join(" ", args));
                    instance.WriteLn("[Chat] <Server>: " + string.Join(" ", args));
                }
            });
        }
    }
}
