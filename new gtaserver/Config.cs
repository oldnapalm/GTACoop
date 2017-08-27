using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer
{

    public class Config
    {

        public int Port { get; set; } = 4499;
        public int MaxPlayers { get; set; } = 16;

        public string GamemodeName { get; set; } = "freeroam";
        public string ServerName { get; set; } = "GTAServer";
        public string Password { get; set; } = "";
        public string MOTD { get; set; } = "Welcome to GTA Coop, have fun in the server!";

        public List<string> MasterServers { get; set; } = new List<string>()
        {
            "http://clan-banderos.de/gta/",
            "https://gtamaster.nofla.me"
        };
        public bool AnnounceToMaster { get; set; } = true;

        public bool AllowNicknames { get; set; } = true;
        public bool OnlineMode { get; set; } = false;

        public bool Query { get; set; } = false;

    }
}
