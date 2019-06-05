using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace Freeroam.Commands
{
    class RespawnCommand : ICommand
    {
        public string CommandName => "respawn";
        public string HelpText => "Respawn at the server \"spawn point\"";
        public bool Restricted => false;

        private GameServer GameServer => Freeroam.GameServer;

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            GameServer.SendNotificationToAll($"~g~{caller.DisplayName} ~s~respawned");

            caller.SendNativeCall(0x891B5B39AC6302AF, 500); // DO_SCREEN_FADE_OUT

            Task.Run(() =>
            {
                Thread.Sleep(500);

                // TODO: allow server to set position
                caller.SendNativeCall(0xAAA34F8A7CB32098, new LocalPlayerArgument()); // CLEAR_PED_TASKS_IMMEDIATELY
                GameServer.SetPlayerPosition(caller, new Vector3(0, 0, 50));

                caller.SendNativeCall(0xD4E8E24955024033, 500); // DO_SCREEN_FADE_IN
            });
        }
    }
}
