using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.PluginAPI.Entities
{
    class ConsoleCommandSender : ICommandSender
    {
        public string DisplayName { get; set; } = "Console";
        public GameServer GameServer { get; set; }

        public void SendMessage(string message)
        {
            GameServer.logger.LogInformation(message);
        }
    }
}
