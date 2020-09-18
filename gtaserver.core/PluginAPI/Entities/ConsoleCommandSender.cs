using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.PluginAPI.Entities
{
    public class ConsoleCommandSender : ICommandSender
    {
        public string DisplayName { get; } = "Console";
        public GameServer GameServer { get; internal set; }

        public virtual void SendMessage(string message)
        {
            GameServer.logger.LogInformation(message);
        }
    }
}
