using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTACoOp
{
    class PlayerList
    {

        public DateTime Pressed;

        public PlayerList()
        {

        }

        public void Tick(Dictionary<long, SyncPed> opponents)
        {
            if (Pressed.AddSeconds(5) < DateTime.Now || Pressed == default(DateTime)) return;

            var players = new List<SyncPed>(opponents.Select(pair => pair.Value));

            var currentplayer = new SyncPed(0, new Vector3(0, 0,0 ), Quaternion.Identity);
            currentplayer.Name = Main.PlayerSettings.Username;
            currentplayer.Latency = Main.Latency;

            players.Add(currentplayer);

            Function.Call(Hash.DRAW_RECT, 0.11, 0.025, 0.2, 0.03, 0, 0, 0, 220);

            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.45, 0.45);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call((Hash)0x25FBB336DF1804CB, "STRING");

            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, $"GTA Coop ({players.Count} online)");

            Function.Call((Hash)0xCD015E5BB0D96A57, 0.015, 0.007);

            int current = 1;
            foreach (var player in players)
            {
                int r, g, b;

                if (current % 2 == 0)
                {
                    r = 28;
                    g = 47;
                    b = 68;
                }
                else
                {
                    r = 38;
                    g = 57;
                    b = 74;
                }

                Function.Call(Hash.DRAW_RECT, 0.11, 0.025 + (current * 0.03), 0.2, 0.03, r, g, b, 220);

                //player name
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_SCALE, 0.45, 0.45);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call((Hash)0x25FBB336DF1804CB, "STRING");

                Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, player.Name);

                Function.Call((Hash)0xCD015E5BB0D96A57, 0.015, 0.007 + (current * 0.03));

                //latency
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_SCALE, 0.45, 0.45);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                Function.Call(Hash.SET_TEXT_WRAP, 0, 0.2);
                Function.Call((Hash)0x25FBB336DF1804CB, "STRING");

                Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, (int)(player.Latency * 1000) + "ms");

                Function.Call((Hash)0xCD015E5BB0D96A57, 0.1, 0.007 + (current * 0.03));

                current++;
            }
        }

    }
}
