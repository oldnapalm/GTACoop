using System.Windows.Forms;

namespace GTACoOp
{
    public class PlayerSettings
    {
        public string Username { get; set; }
        public string LastIP { get; set; }
        public int LastPort { get; set; }
        public string LastPassword { get; set; }
        public bool SyncWorld { get; set; }
        //public TrafficMode SyncTraffic { get; set; }
        public bool DisableTraffic { get; set; }
        public bool DisablePeds { get; set; }
        public bool Logging { get; set; }
        public bool ChatLog { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public string BackupMasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }
        public bool HidePasswords { get; set; }
        public bool AutoConnect { get; set; }
        public bool AutoReconnect { get; set; }
        public string AutoLogin { get; set; }
        public bool AutoRegister { get; set; }
        public bool AutoStartServer { get; set; }
        public bool ShowNetGraph { get; set; }

        public PlayerSettings()
        {
            Username = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
            MaxStreamedNpcs = 10;
            MasterServerAddress = "https://master.gtacoop.com";
            BackupMasterServerAddress = "http://clan-banderos.de/gta/";
            ActivationKey = Keys.F9;
            HidePasswords = false;
            LastIP = "127.0.0.1";
            LastPort = 4499;
            LastPassword = "changeme";
            Logging = false;
            ChatLog = false;
            SyncWorld = false;
            //SyncTraffic = TrafficMode.None;
            DisableTraffic = false;
            DisablePeds = false;
            AutoConnect = false;
            AutoReconnect = true;
            AutoLogin = "";
            AutoRegister = false;
            AutoStartServer = false;
            ShowNetGraph = false;
        }
    }
}