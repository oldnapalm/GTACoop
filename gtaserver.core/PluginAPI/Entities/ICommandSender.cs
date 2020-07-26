using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.PluginAPI.Entities
{
    public interface ICommandSender
    {
        public string DisplayName { get; }

        public void SendMessage(string message);
    }
}
