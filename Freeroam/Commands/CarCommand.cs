using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace Freeroam.Commands
{
    class CarCommand : ICommand
    {
        public string HelpText => "Spawns a car in front of the player";
        public bool Restricted => false;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            Freeroam.GameServer.World.CreateVehicle(-344943009, caller.Position);
        }
    }
}
