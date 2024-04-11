using System;
using System.Drawing;
using Lidgren.Network;
using GTA.Native;
using System.IO;
using LemonUI.Elements;

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

            new ScaledText(new Point(1400, 900), "In:    " + NetUtility.ToHumanReadable(lastBytesReceived) + "/s", 0.5f).Draw();
            new ScaledText(new Point(1400, 930), "Out:  " + NetUtility.ToHumanReadable(lastBytesSent) + "/s", 0.5f).Draw();
            new ScaledText(new Point(1400, 960), "Ping: " + TimeSpan.FromSeconds(Main.Latency).TotalMilliseconds + "ms", 0.5f).Draw();
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

            try
            {
                _handle = new StreamWriter(path + @"\error.log");
            }
            catch(Exception)
            {
                _handle = null;
            }

            WriteLine("GTA Coop v" + (Util.GetAssemblyVersion() ?? Main.ReadableScriptVersion()));
        }

        public void Dispose()
        {
            _handle?.Close();

            GC.SuppressFinalize(this);
        }

        public void Write(string text)
        {
            // not using ?. here due to compiler generating two if statements
            if (_handle != null)
            {
                _handle.Write(text);
                _handle.Flush();
            }
        }

        public void WriteLine(string text = "")
        {
            if (_handle != null)
            {
                _handle.WriteLine(text);
                _handle.Flush();
            }
        }

        public void WriteException(string description, Exception e)
        {
            if (_handle != null)
            {
                _handle.WriteLine(description + ": " + e.ToString());
                _handle.Flush();
            }
        }
    }
}
