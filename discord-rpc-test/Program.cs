using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using discord_rpc_test.discord;

namespace discord_rpc_test
{
    class Program
    {
        public static DiscordRpc.EventHandlers Handlers;
        static DiscordRpc.RichPresence Presence;

        static void Main(string[] args)
        {

            Handlers = new DiscordRpc.EventHandlers();
            Handlers.ReadyCallback = ReadyCallback;
            DiscordRpc.Initialize("348838311873216514", ref Handlers, true, "");

            Presence.Details = "testing";
            Presence.LargeImageKey = "352120722522374144";

            DiscordRpc.UpdatePresence(ref Presence);

            while (true)
            {
                
            }
        }

        private static void ReadyCallback()
        {
            Console.Write("ok");
        }
    }
}
