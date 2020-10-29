using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GTAServer.Commands;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using Sentry;
using GTAServer.PluginAPI.Entities;
using GTAServer.Users;
using System.Runtime.InteropServices;
using GTAServer.Logging;

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
        private static AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private static Timer _timer;

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

            Util.LoggerFactory = new LoggerFactory();
            if (_debugMode)
            {

                Util.LoggerFactory.AddProvider(new DefaultLoggerProvider(LogLevel.Trace));
            }
            else
            {
                Util.LoggerFactory.AddProvider(new DefaultLoggerProvider(LogLevel.Information));
            }
            _logger = Util.LoggerFactory.CreateLogger<ServerManager>();
            DoDebugWarning();

            // enable Sentry
            if(!_debugMode)
            {
                SentrySdk.Init(config =>
                {
                    config.Dsn = new Dsn("https://61668555fb9846bd8a2451366f50e5d3@sentry.io/1320932");

                    // minidumps are only written on Windows as Linux has no such functionality
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        config.AddExceptionProcessor(new MiniDump());
                    }
                });

                SentrySdk.ConfigureScope(scope =>
                {
                    // add configuration to crash reports
                    scope.SetExtra("configuration", _gameServerConfiguration);

                    // if server is ran on Windows we attempt to find a local discord client
                    // we will then get the username of the current user and include this in the event
                    // assuming developers need more information about a crash they can contact the user
                    // (this can be disabled by settings 'AnonymousCrashes' to true in the server configuration)
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !_gameServerConfiguration.AnonymousCrashes)
                    {
                        var user = Discord.GetDiscordUser();
                        if (user != null)
                        {
                            scope.User.Id = user.Id;
                            scope.User.Username = $"{user.Name}#{user.Discriminator}";
                        }
                    }
                });
            }

            if (_gameServerConfiguration.ServerVariables.Any(v => v.Key == "tickEvery"))
            {
                var tpsString = _gameServerConfiguration.ServerVariables.First(v => v.Key == "tickEvery").Value;
                if (!int.TryParse(tpsString, out _tickEvery))
                {
                    _logger.LogError(LogEvent.Setup,
                        "Could not set ticks per second from server variable 'tps' (value is not an integer)");
                }
                else
                {
                    _logger.LogInformation(LogEvent.Setup,
                        "Custom tick rate set. Will try to tick every " + _tickEvery + "ms");
                }
            }
            
            _logger.LogInformation(LogEvent.Setup, "Server preparing to start...");

            _gameServer = new GameServer(_gameServerConfiguration.Port, _gameServerConfiguration.ServerName,
                _gameServerConfiguration.GamemodeName, _debugMode, _gameServerConfiguration.UPnP)
            {
                Password = _gameServerConfiguration.Password,
                AnnounceSelf = _gameServerConfiguration.AnnounceSelf,
                AllowNicknames = _gameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = _gameServerConfiguration.AllowOutdatedClients,
                MaxPlayers = _gameServerConfiguration.MaxClients,
                Motd = _gameServerConfiguration.Motd,
                RconPassword = _gameServerConfiguration.RconPassword
            };

            // push master servers (backwards compatible)
            _gameServer.MasterServers.AddRange(
                new []{ _gameServerConfiguration.PrimaryMasterServer, _gameServerConfiguration.BackupMasterServer});

            _gameServer.Start();

            // Plugin Code
            _logger.LogInformation(LogEvent.Setup, "Loading plugins");
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

            _logger.LogInformation(LogEvent.Setup, "Plugins loaded. Enabling plugins...");
            foreach (var plugin in _plugins)
            {
                if (!plugin.OnEnable(_gameServer, false))
                {
                    _logger.LogWarning(LogEvent.Setup, "Plugin " + plugin.Name + " returned false when enabling, marking as disabled, although it may still have hooks registered and called.");
                }
            }

            // prepare console
            if (!_gameServerConfiguration.NoConsole)
            {
                _cancellationToken = new CancellationTokenSource();
                var console = new ConsoleThread
                {
                    CancellationToken = _cancellationToken.Token,
                    GameServer = _gameServer
                };

                Thread c = new Thread(new ThreadStart(console.ThreadProc)) { Name = "Server console thread" };
                c.Start();

            }

            Console.CancelKeyPress += Console_CancelKeyPress;

            // ready
            _logger.LogInformation(LogEvent.Setup, "Starting server main loop, ready to accept connections.");

            _timer = new Timer(DoServerTick, _gameServer, 0, _tickEvery);
            _autoResetEvent.WaitOne();
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
                _logger.LogError(LogEvent.Tick, e, "Exception while ticking");
                if (_debugMode)
                    // rethrow exception
                    throw;
                else
                {
                    e.Data.Add("TickException", true);
                    SentrySdk.CaptureException(e);
                }
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _logger.LogInformation("^c detected, exiting.");
            _cancellationToken?.Cancel();

            _userModule?.Stop();

            _timer.Dispose();
            _autoResetEvent.Set();

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

    internal class ConsoleThread
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
                lock (GameServer)
                {
                    commands = GameServer.Commands;

                    // continue if the command exists
                    if (!commands.ContainsKey(arguments[0])) continue;

                    // invoke the command
                    commands[arguments[0]].Invoke(new CommandContext
                    {
                        Sender = sender,
                        GameServer = GameServer
                    }, arguments.Skip(1).ToList());
                }
            }
        }
    }
}
