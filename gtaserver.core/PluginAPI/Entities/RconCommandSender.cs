using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GTAServer.PluginAPI.Entities
{
    public class RconCommandSender : ConsoleCommandSender
    {
        public IPEndPoint Destination { get; internal set; }

        public override void SendMessage(string message)
        {
            GameServer.RespondRconMessage(Destination, message);
        }
    }
}
