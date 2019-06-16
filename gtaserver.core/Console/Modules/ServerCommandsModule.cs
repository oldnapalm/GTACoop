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
            });
        }
    }
}
