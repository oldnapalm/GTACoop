using NativeUI;
using System;
using System.Drawing;
using Lidgren.Network;
using GTA.Native;

namespace GTACoOp
{
    public class Debug
    {
        private NetClient _client;

        public NetClient NetClient
        {
            set
            {
                _client = value;

                _client.Statistics.PacketReceived += PacketReceived;
                _client.Statistics.PacketSent += PacketSent;
            }
        }

        private int bytesReceived;
        private int bytesSent;

        private int lastBytesReceived;
        private int lastBytesSent;
        private long lastUpdate;

        private void PacketReceived(object sender, PacketEventArgs e)
        {
            bytesReceived += e.NumBytes;
        }

        private void PacketSent(object sender, PacketEventArgs e)
        {
            bytesSent += e.NumBytes;
        }

        public bool Enabled { get; set; }

        public Debug()
        {
            lastUpdate = GetGameTimer();
        }

        public void Tick()
        {
            if (!Enabled) return;
            if (!Main.IsOnServer()) return;
            if (_client == null) return;

            var time = GetGameTimer();
            if ((time - lastUpdate) > 1000)
            {
                lastUpdate = time;

                lastBytesReceived = bytesReceived;
                lastBytesSent = bytesSent;

                bytesReceived = 0;
                bytesSent = 0;
            }

            new UIResText("In:    " + NetUtility.ToHumanReadable(lastBytesReceived) + "/s", new Point(1400, 900), 0.5f).Draw();
            new UIResText("Out:  " + NetUtility.ToHumanReadable(lastBytesSent) + "/s", new Point(1400, 930), 0.5f).Draw();
            new UIResText("Ping: " + TimeSpan.FromSeconds(Main.Latency).TotalMilliseconds + "ms", new Point(1400, 960), 0.5f).Draw();
        }

        public long GetGameTimer()
        {
            return Function.Call<long>(Hash.GET_GAME_TIMER);
        }
    }
}
