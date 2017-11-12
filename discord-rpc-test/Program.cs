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
            DiscordRpc.Initialize("348838311873216514", ref Handlers, true, "");

            Presence.Details = "Connected to server";
            Presence.LargeImageKey = "large_logo";
            //Presence.SmallImageKey = "franklin";
            //Presence.LargeImageText = "Playing as Franklin";
            Presence.PartySize = 4;
            Presence.PartyMax = 16;
            Presence.State = "127.0.0.1:4499";

            DiscordRpc.UpdatePresence(ref Presence);

            while (true)
            {
                
            }
        }
    }
}
