using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GTAServer.Commands;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using SimpleConsoleLogger;
using Sentry;
using GTAServer.PluginAPI.Entities;
using GTAServer.Users;

namespace GTAServer
{
    public class ServerManager
    {
        private static ServerConfiguration _gameServerConfiguration;
        private static GameServer _gameServer;
        private static ILogger _logger;
        private static readonly List<IPlugin> _plugins = new List<IPlugin>();
        private static readonly string _location = System.AppContext.BaseDirectory;

        private static bool _debugMode = false;
        private static int _tickEvery = 10;

        private static UserModule _userModule;

        private static CancellationTokenSource _cancellationToken;

        private static void CreateNeededFiles()
        {
            if (!Directory.Exists(Path.Combine(_location, "Plugins")))
                Directory.CreateDirectory(Path.Combine(_location, "Plugins"));

            if (!Directory.Exists(Path.Combine(_location, "Configuration")))
                Directory.CreateDirectory(Path.Combine(_location, "Configuration"));
        }

        private static void DoDebugWarning()
        {
            if (!_debugMode) return;
            _logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
            _logger.LogWarning("Furthermore, debug builds will not announce themselves to the master server, regardless of the AnnounceSelf config option.");
            _logger.LogWarning("To help bring crashes to the attention of the server owner and make sure they are reported to me, error catching has been disabled in this build.");
        }

        public static void Main(string[] args)
        {
#if DEBUG
            _debugMode = true;
#endif

            CreateNeededFiles();

            // can't use logger here since the logger config depends on if debug mode is on or off
            Console.WriteLine("Reading server configuration...");

            _gameServerConfiguration =
                LoadServerConfiguration(Path.Combine(_location, "Configuration", "serverSettings.xml"));
            if (!_debugMode) _debugMode = _gameServerConfiguration.DebugMode;

            if (_debugMode)
            {

                Util.LoggerFactory = new LoggerFactory()
                    .AddSimpleConsole();
            }
            else
            {
                Util.LoggerFactory = new LoggerFactory()
                    .AddSimpleConsole((s, l) => (int) l >= (int) LogLevel.Information);
            }
            _logger = Util.LoggerFactory.CreateLogger<ServerManager>();
            DoDebugWarning();

            // enable Sentry
            if(!_debugMode)
            {
                SentrySdk.Init("https://61668555fb9846bd8a2451366f50e5d3@sentry.io/1320932");

                SentrySdk.ConfigureScope(scope =>
                {
                    // add configuration to crash reports
                    scope.SetExtra("configuration", _gameServerConfiguration);
                });
            }

            if (_gameServerConfiguration.ServerVariables.Any(v => v.Key == "tickEvery"))
            {
                var tpsString = _gameServerConfiguration.ServerVariables.First(v => v.Key == "tickEvery").Value;
                if (!int.TryParse(tpsString, out _tickEvery))
                {
                    _logger.LogError(
                        "Could not set ticks per second from server variable 'tps' (value is not an integer)");
                }
                else
                {
                    _logger.LogInformation("Custom tick rate set. Will try to tick every " + _tickEvery + "ms");
                }
            }
            
            _logger.LogInformation("Server preparing to start...");

            _gameServer = new GameServer(_gameServerConfiguration.Port, _gameServerConfiguration.ServerName,
                _gameServerConfiguration.GamemodeName, _debugMode, _gameServerConfiguration.UPnP)
            {
                Password = _gameServerConfiguration.Password,
                AnnounceSelf = _gameServerConfiguration.AnnounceSelf,
                AllowNicknames = _gameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = _gameServerConfiguration.AllowOutdatedClients,
                MaxPlayers = _gameServerConfiguration.MaxClients,
                Motd = _gameServerConfiguration.Motd
            };

            // push master servers (backwards compatible)
            _gameServer.MasterServers.AddRange(
                new []{ _gameServerConfiguration.PrimaryMasterServer, _gameServerConfiguration.BackupMasterServer});

            _gameServer.Start();

            // Plugin Code
            _logger.LogInformation("Loading plugins");
            //Plugins = PluginLoader.LoadPlugin("TestPlugin");
            foreach (var pluginName in _gameServerConfiguration.ServerPlugins)
            {
                foreach (var loadedPlugin in PluginLoader.LoadPlugin(pluginName))
                {
                    _plugins.Add(loadedPlugin);
                }
            }

            // TODO future refactor
            if (_gameServerConfiguration.UseGroups)
            {
                _userModule = new UserModule(_gameServer);
                _userModule.Start();
				
                _gameServer.PermissionProvider = _userModule;
            }

            RegisterCommands();

            _logger.LogInformation("Plugins loaded. Enabling plugins...");
            foreach (var plugin in _plugins)
            {
                if (!plugin.OnEnable(_gameServer, false))
                {
                    _logger.LogWarning("Plugin " + plugin.Name + " returned false when enabling, marking as disabled, although it may still have hooks registered and called.");
                }
            }

            // prepare console
            _cancellationToken = new CancellationTokenSource();
            var console = new ConsoleThread
            {
                CancellationToken = _cancellationToken.Token,
                GameServer = _gameServer
            };

            Console.CancelKeyPress += Console_CancelKeyPress;
            Thread c = new Thread(new ThreadStart(console.ThreadProc)) { Name = "Server console thread" };
            c.Start();

            // ready
            _logger.LogInformation("Starting server main loop, ready to accept connections.");

            var t = new Timer(DoServerTick, _gameServer, 0, _tickEvery);
            while(true) Thread.Sleep(1);
        }

        public static void DoServerTick(object serverObject)
        {
            var server = (GameServer)serverObject;

            try
            {
                server.Tick();
            }
            catch (Exception e)
            {
                _logger.LogError("Exception while ticking", e.Message);
                if (_debugMode)
                    // rethrow exception
                    throw;
                else
                    SentrySdk.CaptureException(e);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _logger.LogInformation("^c detected, exiting.");
            _cancellationToken.Cancel();

            _userModule?.Stop();

            Environment.Exit(0);
        }

        private static ServerConfiguration LoadServerConfiguration(string path)
        {
            var ser = new XmlSerializer(typeof(ServerConfiguration));

            ServerConfiguration cfg = null;
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) cfg = (ServerConfiguration)ser.Deserialize(stream);
                using (
                    var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create,
                        FileAccess.ReadWrite)) ser.Serialize(stream, cfg);
            }
            else
            {
                Console.WriteLine("No configuration found, creating a new one.");
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, cfg = new ServerConfiguration());
            }

            return cfg;
        }

        // register server commands
        private static void RegisterCommands()
        {
            _gameServer.RegisterCommands<UserCommands>();
            _gameServer.RegisterCommands<AdminCommands>();
            _gameServer.RegisterCommands<InfoCommands>();
        }
    }

    class ConsoleThread
    {
        public CancellationToken CancellationToken { get; set; }
        public GameServer GameServer { get; set; }

        public void ThreadProc()
        {
            var sender = new ConsoleCommandSender
            {
                GameServer = GameServer
            };

            // continue until we're told to stop
            while (!CancellationToken.IsCancellationRequested)
            {
                // read the input from the console and attempt to parse it as command
                // this will find the command in the gameserver and execute if it exist

                var input = Console.ReadLine();
                if (input == null) continue;
                // TODO this needs to take quotes into account
                var arguments = input.Split(" ");

                Dictionary<string, Action<CommandContext, List<string>>> commands;
                lock (GameServer.Commands) commands = GameServer.Commands;

                // continue if the command exists
                if (!commands.ContainsKey(arguments[0])) continue;

                // invoke the command
                commands[arguments[0]].Invoke(new CommandContext
                {
                    Sender = sender                                                             ,
                    GameServer = GameServer
                }, arguments.Skip(1).ToList());
            }
        }
    }
}
