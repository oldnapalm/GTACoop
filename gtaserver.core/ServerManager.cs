using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using GTAServer.Commands;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using Sentry;
using GTAServer.PluginAPI.Entities;
using GTAServer.Users;
using System.Runtime.InteropServices;
using GTAServer.Logging;
using ProtoBuf;

namespace GTAServer
{
    public class ServerManager
    {
        private const string SENTRY_DSN = "https://61668555fb9846bd8a2451366f50e5d3@sentry.io/1320932";

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

            if (!Directory.Exists(Path.Combine(_location, "Data")))
                Directory.CreateDirectory(Path.Combine(_location, "Data"));
        }

        private static void DoDebugWarning()
        {
            if (!_debugMode) return;
            _logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
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

            // initialize error tracking
            if (!_debugMode)
            {
                var sentry = SentrySdk.Init(config => 
                {
                    config.Dsn = SENTRY_DSN;
                    config.AutoSessionTracking = true;

                    // write minidumps on Windows
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) config.AddExceptionProcessor(new MiniDump());
                });

                ConfigureErrorTracking();
            }

            // continue
            Start();
        }

        private static void Start()
        {
            Util.LoggerFactory = new LoggerFactory();
            Util.CreateHttpClient();

            if (_debugMode)
            {

                Util.LoggerFactory.AddProvider(new ConsoleLoggerProvider(LogLevel.Trace));
            }
            else
            {
                Util.LoggerFactory.AddProvider(new ConsoleLoggerProvider(LogLevel.Information));
            }
            _logger = Util.LoggerFactory.CreateLogger<ServerManager>();

            DoDebugWarning();

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
                _gameServerConfiguration.GamemodeName, _debugMode, _gameServerConfiguration)
            {
                Password = _gameServerConfiguration.Password,
                AnnounceSelf = _gameServerConfiguration.AnnounceSelf,
                AllowNicknames = _gameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = false, //_gameServerConfiguration.AllowOutdatedClients,
                MaxPlayers = _gameServerConfiguration.MaxClients,
                Motd = _gameServerConfiguration.Motd,
                RconPassword = _gameServerConfiguration.RconPassword
            };

            // push master servers (backwards compatible)
            _gameServer.MasterServers.AddRange(
                new []{ _gameServerConfiguration.PrimaryMasterServer, _gameServerConfiguration.BackupMasterServer});

            _gameServer.Start();

            // Plugin Code
            _logger.LogInformation(LogEvent.Plugin, "Loading plugins");

            foreach (var pluginName in _gameServerConfiguration.ServerPlugins)
            {
                foreach (var loadedPlugin in PluginLoader.LoadPlugin(pluginName))
                {
                    _plugins.Add(loadedPlugin);
                }
            }

            RegisterCommands();

            // TODO future refactor
            if (_gameServerConfiguration.UseGroups)
            {
                _userModule = new UserModule(_gameServer);
                _gameServer.PermissionProvider = _userModule;

                _userModule.Start();
            }
            _gameServer.Metrics = new PrometheusMetrics();

            _logger.LogInformation(LogEvent.Plugin, "Plugins loaded. Enabling plugins...");
            foreach (var plugin in _plugins)
            {
                if (!plugin.OnEnable(_gameServer, false))
                {
                    _logger.LogWarning(LogEvent.Plugin, "Plugin " + plugin.Name + " returned false when enabling, marking as disabled, although it may still have hooks registered and called.");
                }
            }

            // prepare console
            _cancellationToken = new CancellationTokenSource();
            var console = new ConsoleThread
            {
                CancellationToken = _cancellationToken.Token
            };

            Thread c = new Thread(new ThreadStart(console.ThreadProc)) { Name = "Server console thread" };
            c.Start();

            // ready
            _logger.LogInformation(LogEvent.Setup, "Starting server main loop, ready to accept connections.");

            _timer = new Timer(DoServerTick, _gameServer, 0, _tickEvery);

            Console.CancelKeyPress += Console_CancelKeyPress;
            _autoResetEvent.WaitOne();
        }

        public static void DoServerTick(object serverObject)
        {
            var server = (GameServer)serverObject;

            try
            {
                server.Tick();
            }
            catch(ProtoException) { }
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
            _logger.LogInformation("SIGINT received - exiting");
            _cancellationToken?.Cancel();

            _userModule?.Stop();

            _timer.Dispose();
            _autoResetEvent.Set();

            Util.LoggerFactory.Dispose();

            Environment.Exit(0);
        }

        private static ServerConfiguration LoadServerConfiguration(string path)
        {
            var ser = new XmlSerializer(typeof(ServerConfiguration));

            ServerConfiguration cfg = null;
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                cfg = (ServerConfiguration)ser.Deserialize(stream);
            }
            else
            {
                Console.WriteLine("No configuration found, creating a new one.");

                using var stream = File.OpenWrite(path);

                var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true });
                ser.Serialize(writer, cfg = new ServerConfiguration());
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

        /// <summary>
        /// Executes a command on the current <see cref="GameServer"/> instance,
        /// </summary>
        /// <param name="cmd">The raw command to execute</param>
        /// <returns>Whether the command existed</returns>
        public static bool ExecuteCommand(string cmd, GameServer server = null, ICommandSender sender = null)
        {
            server = server ?? _gameServer;

            if (sender == null)
            {
                sender = new ConsoleCommandSender
                {
                    GameServer = server
                };
            }

            var arguments = Util.SplitCommandString(cmd);

            Dictionary<Command, Action<CommandContext, List<string>>> commands;
            lock (server)
            {
                commands = server.Commands;

                // check if the command exists
                if (arguments.Count == 0 || !commands.Any(x => x.Key.Name == arguments[0])) return false;

                // invoke the command
                var command = commands.First(x => x.Key.Name == arguments[0]);
                var context = new CommandContext { Sender = sender, GameServer = _gameServer };

                command.Value.Invoke(context, arguments.Skip(1).ToList());

                return true;
            }

        }

        private static void ConfigureErrorTracking()
        {
            SentrySdk.ConfigureScope(scope =>
            {
                // add configuration to crash reports
                scope.SetExtra("configuration", _gameServerConfiguration);
                scope.SetExtra("path", AppContext.BaseDirectory);
            });
        }
    }

    internal class ConsoleThread
    {
        public CancellationToken CancellationToken { get; set; }

        public void ThreadProc()
        {
            // continue until we're told to stop
            while (!CancellationToken.IsCancellationRequested)
            {
                // read the input from the console and attempt to parse it as command
                // this will find the command in the gameserver and execute if it exist

                var input = Console.ReadLine();
                if (input == null) continue;

                ServerManager.ExecuteCommand(input);
            }
        }
    }
}
