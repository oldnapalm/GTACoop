using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace GTAServer
{
    [XmlType(TypeName = "ServerVariable")]
    public struct ServerVariable
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class ServerConfiguration
    {
        public int Port { get; set; } = 4499;
        public int MaxClients { get; set; } = 16;
        public string GamemodeName { get; set; } = "none";
        public string ServerName { get; set; } = "GTACoOp Server";
        public string Password { get; set; } = "";
        public string PrimaryMasterServer { get; set; } = "https://master.gtacoop.com/";
        public string BackupMasterServer { get; set; } = "http://clan-banderos.de/gta/";
        public bool AnnounceSelf { get; set; } = true;
        public bool AllowNicknames { get; set; } = true;
        public bool AllowOutdatedClients { get; set; } = false;
        public bool DebugMode { get; set; } = false;

        public string Motd { get; set; } = "Welcome to this GTA CooP server!";
        public bool UseGroups { get; set; } = true;

        public List<string> ServerPlugins { get; set; } = new List<string>() {};

        public List<ServerVariable> ServerVariables = new List<ServerVariable>();
    }
}
