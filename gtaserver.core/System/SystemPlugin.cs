using gtaserver.core.Commands;
using GTAServer;
using GTAServer.PluginAPI;
using System;
using System.Collections.Generic;
using System.Text;

namespace gtaserver.core.System
{
    class SystemPlugin : IPlugin
    {
        public string Name => "System";

        public string Description => "This is a default plugin and can't be disabled it's used for managing server features like built-in commands.";

        public string Author => "TheIndra";

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            gameServer.Commands.Add("help", new HelpCommand());
            gameServer.Commands.Add("tps", new TpsCommand());
            gameServer.Commands.Add("about", new AboutCommand());
            gameServer.Commands.Add("plugins", new PluginsCommand());

            return true;
        }
    }
}
