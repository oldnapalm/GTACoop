using NativeUI;
using System;
using System.Drawing;
using Lidgren.Network;
using GTA.Native;
using System.IO;

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

    // very basic logger
    public class Logger : IDisposable
    {
        private StreamWriter _handle;

        public Logger()
        {
            // %localappdata%\cgmp\gtacoop\error.log
            // C:\Program Files\ can not always be written
            // documents might be easier to find for the user?
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cgmp", "gtacoop");

            if(!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // TODO this might fail and then entire client is fucked, try to catch
            _handle = new StreamWriter(path + @"\error.log");

            // yay
            WriteLine("GTA Coop version " + Main.ReadableScriptVersion());
        }

        public void Dispose()
        {
            _handle.Close();

            GC.SuppressFinalize(this);
        }

        public void Write(string text)
        {
            _handle.Write(text);
            _handle.Flush();
        }

        public void WriteLine(string text = "")
        {
            _handle.WriteLine(text);
            _handle.Flush();
        }

        public void WriteException(string description, Exception e)
        {
            _handle.WriteLine(description + ": " + e.Message + "\n" 
                + e.StackTrace);
            _handle.Flush();
        }
    }
}
