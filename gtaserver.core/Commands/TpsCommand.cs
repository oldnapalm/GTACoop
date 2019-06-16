using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;

namespace GTAServer.Commands
{
    class TpsCommand : ICommand
    {
        public string CommandName => "tps";

        public string HelpText => "Shows the server tick per seconds";

        public void OnCommandExec(Client caller, ChatData chatData)
        {
            caller.SendMessage("TPS: " + ServerManager.GameServer.TicksPerSecond);
        }
    }
}
