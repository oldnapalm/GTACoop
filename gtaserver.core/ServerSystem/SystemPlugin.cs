using gtaserver.core.Commands;
using GTAServer;
using GTAServer.PluginAPI;

namespace gtaserver.core.ServerSystem
{
    class SystemPlugin : IPlugin
    {
        public string Name => "System";
        public string Description => "This is a default plugin and can't be disabled it's used for managing server features like built-in commands.";
        public string Author => "TheIndra";

        public static GameServer GameServer;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            _gameServer = gameServer;

            // register built-in commands
            gameServer.Commands.Add("help", new HelpCommand());
            gameServer.Commands.Add("tps", new TpsCommand());
            gameServer.Commands.Add("about", new AboutCommand());
            gameServer.Commands.Add("plugins", new PluginsCommand());
            gameServer.Commands.Add("say", new SayCommand());

            gameServer.Commands.Add("kick", new KickCommand());

            return true;
        }
    }
}
