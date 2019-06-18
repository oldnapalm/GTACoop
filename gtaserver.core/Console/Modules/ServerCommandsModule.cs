using System;
using System.Collections.Generic;
using System.Text;

namespace GTAServer.Console.Modules
{
    class ServerCommandsModule : IModule
    {
        public void OnEnable(ConsoleInstance instance)
        {
            instance.AddCommand("about", args =>
            {
                instance.WriteLn("This server is running GTAServer.core");
                // TODO: version and info
            });

            instance.AddCommand("tps", args =>
            {
                instance.WriteLn("TPS: " + ServerManager.GameServer.TicksPerSecond);
            });

            instance.AddCommand("version", args =>
            {
                // TODO: add version stuff
            });
        }
    }
}
