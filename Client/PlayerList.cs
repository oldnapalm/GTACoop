using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GTACoOp
{
    class PlayerList
    {
        private Scaleform _scaleform;
        private long _lastUpdate;

        public long Pressed;

        public PlayerList()
        {
            _scaleform = new Scaleform("mp_mm_card_freemode");
        }

        // don't call this at high rate unless you want your game to crash
        public void Update(Dictionary<long, SyncPed> opponents)
        {
            _scaleform.CallFunction("SET_DATA_SLOT_EMPTY", 0);
            _scaleform.CallFunction("SET_DATA_SLOT", 0, $"{Main.Latency * 1000:N0}ms", Main.PlayerSettings.Username, 116, 0, "", "", "", 2, "", "", ' ');

            int i = 1;
            foreach(var opponent in opponents)
            {
                var player = opponent.Value;

                _scaleform.CallFunction("SET_DATA_SLOT", i++, $"{player.Latency * 1000:N0}ms", player.Name, 116, 0, "", "", "", 2, "", "", ' ');
            }

            _scaleform.CallFunction("SET_TITLE", "GTA Coop", (Main.Opponents.Count + 1) + " players");
            _scaleform.CallFunction("DISPLAY_VIEW");

            _lastUpdate = GetGameTimer();
        }

        public void Tick(bool draw)
        {
            if (draw && (GetGameTimer() - Pressed) < 5000 && Main.IsOnServer())
            {
                Function.Call(Hash.DRAW_SCALEFORM_MOVIE, _scaleform.Handle, 0.122f, 0.3f, 0.28f, 0.6f, 255, 255, 255, 255, 0);
            }

            // update ping every second
            if ((GetGameTimer() - _lastUpdate) > 1000)
            {
                Update(Main.Opponents);
            }
        }

        public long GetGameTimer()
        {
            return Function.Call<long>(Hash.GET_GAME_TIMER);
        }
    }
}
