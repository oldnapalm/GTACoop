using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using Lidgren.Network;
using NativeUI;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using System.Text.RegularExpressions;
using System.Globalization;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

#if VOICE
using NAudio.Wave;
#endif

namespace GTACoOp
{
    public class Main : Script
    {
        public static PlayerSettings PlayerSettings;
        public static ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9_4;

        private readonly UIMenu _mainMenu;
        public UIMenu _serverBrowserMenu;
        //private readonly UIMenu _playerMenu;
        private readonly UIMenu _settingsMenu;
        private readonly MenuPool _menuPool;

        private string _clientIp;
        private string _masterIP;
        private readonly Chat _chat;

        private static NetClient _client;
        private static NetPeerConfiguration _config;

        private static int _channel;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private bool _lastDead;
        private bool _wasTyping;
        private DebugWindow _debug;

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        private string _lastIP = "";
        private int _lastPort = 4499;
        //

        private bool debug;

        public static Debug DebugLogger;
        private static Logger _logger;

        private bool _isGoingToCar;

        public static Dictionary<long, SyncPed> Opponents;
        public static Dictionary<string, SyncPed> Npcs;
        public static float Latency;
        private int Port;

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;
        private List<int> _entityCleanup;
        private List<int> _blipCleanup;

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();

        private static PlayerList _playerList;

        private static UIMenuItem _passItem;

#if VOICE
        // NAUDIO
        private static WaveInEvent _waveInput;
        private static WaveOut _waveOutput;
        private static WaveFileWriter _waveWriter;

        private static BufferedWaveProvider _playBuffer;

        private static bool _isTalking = false;
#endif

        private enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1,
            ReturnsEntity = 2,
            ReturnsEntityNeedsModel1 = 3,
            ReturnsEntityNeedsModel2 = 4,
            ReturnsEntityNeedsModel3 = 5,
        }

        public static Logger Logger
        {
            get
            {
                return _logger;
            }
        }

        public static string ReadableScriptVersion()
        {
             return Regex.Replace(Regex.Replace(LocalScriptVersion.ToString(), "VERSION_", "", RegexOptions.IgnoreCase), "_", ".", RegexOptions.IgnoreCase);
        }

        public Main()
        {
#if VOICE
            //NAUDIO
            _waveInput = new WaveInEvent();
            _waveOutput = new WaveOut();
            _waveInput.BufferMilliseconds = 15;
            _waveInput.NumberOfBuffers = 3;

            // Sets the input device is the default one.
			_waveInput.DeviceNumber = 0;
            _waveInput.DataAvailable += SendVoiceData;
            _waveInput.WaveFormat = new WaveFormat(44200, 2);
            _waveInput.RecordingStopped += (object sender, StoppedEventArgs e) =>
            {
                _waveInput.Dispose();
                _waveInput = null;
            };

            var voiceStream = new MemoryStream();
            _waveWriter = new WaveFileWriter(voiceStream, _waveInput.WaveFormat);

            _playBuffer = new BufferedWaveProvider(_waveInput.WaveFormat);

            _waveOutput.Init(_playBuffer);
            _waveOutput.Play();
#endif
            PlayerSettings = Util.ReadSettings(Program.Location + Path.DirectorySeparatorChar + "ClientSettings.xml");
            _threadJumping = new Queue<Action>();

            Opponents = new Dictionary<long, SyncPed>();
            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            _entityCleanup = new List<int>();
            _blipCleanup = new List<int>();

            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++) _emptyVehicleMods.Add(i, 0);

            _chat = new Chat();
            _chat.OnComplete += (sender, args) =>
            {
                var message = _chat.CurrentInput;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var obj = new ChatData()
                    {
                        Message = message,
                    };
                    var data = SerializeBinary(obj);

                    var msg = _client.CreateMessage();
                    msg.Write((int)PacketType.ChatData);
                    msg.Write(data.Length);
                    msg.Write(data);
                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 1);
                }
                _chat.IsFocused = false;
            };

            Tick += OnTick;
            Aborted += OnShutdown;
            KeyDown += OnKeyDown;

            KeyUp += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape && _wasTyping)
                {
                    _wasTyping = false;
                }
            };

            _config = new NetPeerConfiguration("GTAVOnlineRaces");
            _config.Port = 8888;
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            #region Menu Set up
            // #warning Affects performance when open, drops from 80~100 on a GTX 980 to high 30s ~ 60
            _menuPool = new MenuPool();

            _mainMenu = new UIMenu("GTA CooP", "MAIN MENU");
            _settingsMenu = new UIMenu("GTA CooP", "CLIENT SETTINGS");
            _serverBrowserMenu = new UIMenu("GTA CooP", "SERVER BROWSER");
            //_playerMenu = new UIMenu("Co-oP", "PLAYER OPTIONS");

            var browserItem = new UIMenuItem("Server Browser");
            browserItem.Activated += (sender, item) => RebuildServerBrowser();

            var lanItem = new UIMenuItem("LAN");
            lanItem.Activated += (sender, item) => DiscoverLan();

            _serverBrowserMenu.SetMenuWidthOffset(300);

            var listenItem = new UIMenuItem("Server IP");
            listenItem.SetRightLabel(PlayerSettings.LastIP);
            listenItem.Activated += (menu, item) =>
            {
                _clientIp = Game.GetUserInput(listenItem.RightLabel, 255);
                if (!string.IsNullOrWhiteSpace(_clientIp))
                {
                    PlayerSettings.LastIP = _clientIp;
                    Util.SaveSettings(null);
                    listenItem.SetRightLabel(PlayerSettings.LastIP);
                }
            };

            var portItem = new UIMenuItem("Port");
            portItem.SetRightLabel(PlayerSettings.LastPort.ToString());
            Port = PlayerSettings.LastPort;
            portItem.Activated += (menu, item) =>
            {
                string newPort = Game.GetUserInput(Port.ToString(), 5);
                int nPort; bool success = int.TryParse(newPort, out nPort);
                if (!success)
                {
                    UI.Notify("Wrong port format.");
                    return;
                }
                Port = nPort;
                PlayerSettings.LastPort = nPort;
                Util.SaveSettings(null);
                portItem.SetRightLabel(nPort.ToString());
            };
            
            _passItem = new UIMenuItem("Password");
            if (PlayerSettings.HidePasswords)
            {
                _passItem.SetRightLabel(new String('*', PlayerSettings.LastPassword.Length));
            }
            else
            {
                _passItem.SetRightLabel(PlayerSettings.LastPassword.ToString());
            }

            _password = PlayerSettings.LastPassword;

            _passItem.Activated += (menu, item) =>
            {
                string lastPassword = Game.GetUserInput(_passItem.RightLabel, 255);
                if (!string.IsNullOrEmpty(lastPassword))
                {
                    PlayerSettings.LastPassword = lastPassword;
                    Util.SaveSettings(null);
                    if (PlayerSettings.HidePasswords)
                    {
                        _passItem.SetRightLabel(new String('*', lastPassword.Length));
                    }
                    else
                    {
                        _passItem.SetRightLabel(lastPassword);
                    }

                    _password = lastPassword;
                }
            };

            var connectItem = new UIMenuItem("Connect");
            _clientIp = PlayerSettings.LastIP;
            connectItem.Activated += (sender, item) =>
            {
                if (!IsOnServer())
                {
                    if (string.IsNullOrEmpty(_clientIp))
                    {
                        UI.Notify("No IP adress specified.");
                        return;
                    }

                    ConnectToServer(_clientIp, Port);
                }
                else
                {
                    if (_client != null) _client.Disconnect("Connection closed by peer.");
                }
            };

            var settItem = new UIMenuItem("Client Settings");

            var aboutItem = new UIMenuItem("~g~GTA V~w~ Coop mod v" + ReadableScriptVersion() + " by ~b~community~w~");
            aboutItem.Activated += (menu, item) =>
            {
                UI.Notify("GTA V Coop mod version " + ReadableScriptVersion());
                UI.Notify("Credits: Guad, Bluscream, wolfmitchell, TheIndra, oldnapalm, EntenKoeniq");
                UI.Notify("Website: www.gtacoop.com");
            };

            _mainMenu.AddItem(browserItem);
            _mainMenu.AddItem(lanItem);
            _mainMenu.AddItem(connectItem);
            _mainMenu.AddItem(listenItem);
            _mainMenu.AddItem(portItem);
            _mainMenu.AddItem(_passItem);
            _mainMenu.AddItem(settItem);
            _mainMenu.AddItem(aboutItem);

            _mainMenu.BindMenuToItem(_serverBrowserMenu, browserItem);
            _mainMenu.BindMenuToItem(_serverBrowserMenu, lanItem);

            _mainMenu.BindMenuToItem(_settingsMenu, settItem);

            var nameItem = new UIMenuItem("Display Name");
            nameItem.SetRightLabel(PlayerSettings.Username);
            nameItem.Activated += (menu, item) =>
            {
                string _DisplayName = Game.GetUserInput(32);
                if (!string.IsNullOrWhiteSpace(_DisplayName))
                {
                    PlayerSettings.Username = _DisplayName;
                    Util.SaveSettings(null);
                    nameItem.SetRightLabel(PlayerSettings.Username);
                }
            };

#if VOICE
            var inputDeviceItem = WaveIn.DeviceCount > 0 ? new UIMenuListItem("Input device", GetInputDevices(), 0) : null;
#endif

            var masterItem = new UIMenuItem("Master Server");
            masterItem.SetRightLabel(PlayerSettings.MasterServerAddress);
            masterItem.Activated += (menu, item) =>
            {
                _masterIP = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_masterIP))
                {
                    PlayerSettings.MasterServerAddress = _masterIP;
                    Util.SaveSettings(null);
                    masterItem.SetRightLabel(PlayerSettings.MasterServerAddress);
                }
            };

            var backupMasterItem = new UIMenuItem("Backup Master Server");
            backupMasterItem.SetRightLabel(PlayerSettings.BackupMasterServerAddress);
            backupMasterItem.Activated += (menu, item) =>
            {
                var _input = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_input))
                {
                    PlayerSettings.BackupMasterServerAddress = _input;
                    Util.SaveSettings(null);
                    backupMasterItem.SetRightLabel(PlayerSettings.BackupMasterServerAddress);
                }
            };

            var chatLogItem = new UIMenuCheckboxItem("Log Chats", PlayerSettings.ChatLog);
            chatLogItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.ChatLog = check;
                Util.SaveSettings(null);
            };

            var hidePasswordsItem = new UIMenuCheckboxItem("Hide Passwords (Restart required)", PlayerSettings.HidePasswords);
            hidePasswordsItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.HidePasswords = check;
                //_mainMenu.RefreshIndex();_settingsMenu.RefreshIndex();_serverMenu.RefreshIndex();
                Util.SaveSettings(null);
            };

            var npcItem = new UIMenuCheckboxItem("Share NPC's With Players", PlayerSettings.SyncWorld);
            npcItem.CheckboxEvent += (item, check) =>
            {
                if (!check && _client != null)
                {
                    var msg = _client.CreateMessage();
                    var obj = new PlayerDisconnect();
                    obj.Id = _client.UniqueIdentifier;
                    var bin = SerializeBinary(obj);

                    msg.Write((int)PacketType.WorldSharingStop);
                    msg.Write(bin.Length);
                    msg.Write(bin);

                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 3);
                }
                PlayerSettings.SyncWorld = check;
                Util.SaveSettings(null);
            };

            /*var trafficItem = new UIMenuCheckboxItem("Enable Traffic When Sharing", PlayerSettings.SyncTraffic, "May affect performance.");
            trafficItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.SyncTraffic = check;
                Util.SaveSettings(null);
            };*/

            var trafficModes = new List<dynamic>(Enum.GetNames(typeof(TrafficMode)));
            var selectedTraffic = trafficModes.IndexOf(PlayerSettings.SyncTraffic.ToString());
            if (selectedTraffic < 0) selectedTraffic = 0;
            var trafficItem = new UIMenuListItem("Share Traffic With Players", trafficModes, selectedTraffic);
            trafficItem.OnListChanged += (item, index) =>
            {
                PlayerSettings.SyncTraffic = (TrafficMode) Enum.Parse(typeof(TrafficMode), item.Items[index].ToString());
                Util.SaveSettings(null);
            };

            var autoConnectItem = new UIMenuCheckboxItem("Auto Connect On Startup", PlayerSettings.AutoConnect);
            autoConnectItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoConnect = check;
                Util.SaveSettings(null);
            };

            var autoReconnectItem = new UIMenuCheckboxItem("Auto Reconnect", PlayerSettings.AutoReconnect);
            autoReconnectItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoReconnect = check;
                Util.SaveSettings(null);
            };
            var autoLoginItem = new UIMenuItem("Auto Login");
            if (PlayerSettings.HidePasswords)
            {
                autoLoginItem.SetRightLabel(new String('*', PlayerSettings.AutoLogin.Length));
            }
            else
            {
                autoLoginItem.SetRightLabel(PlayerSettings.AutoLogin.ToString());
            }
            autoLoginItem.Activated += (menu, item) =>
            {
                string _AutoLogin = Game.GetUserInput(255);
                if (!string.IsNullOrEmpty(_AutoLogin))
                {
                    PlayerSettings.AutoLogin = _AutoLogin;
                    Util.SaveSettings(null);
                    if (PlayerSettings.HidePasswords)
                    {
                        autoLoginItem.SetRightLabel(new String('*', PlayerSettings.AutoLogin.Length));
                    }
                    else
                    {
                        autoLoginItem.SetRightLabel(PlayerSettings.AutoLogin.ToString());
                    }
                }
            };

            var autoRegisterItem = new UIMenuCheckboxItem("Auto Register", PlayerSettings.AutoRegister);
            autoRegisterItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoRegister = check;
                Util.SaveSettings(null);
            };

            var netGraphItem = new UIMenuCheckboxItem("Show Network Info", PlayerSettings.ShowNetGraph);
            netGraphItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.ShowNetGraph = check;
                DebugLogger.Enabled = check;

                Util.SaveSettings(null);
            };

            var debugItem = new UIMenuCheckboxItem("Debug", false);
            debugItem.CheckboxEvent += (item, check) =>
            {
                debug = check;
            };

            _settingsMenu.AddItem(nameItem);
            _settingsMenu.AddItem(npcItem);
            _settingsMenu.AddItem(trafficItem);
#if VOICE
            if (inputDeviceItem != null)
                _settingsMenu.AddItem(inputDeviceItem);
#endif
            _settingsMenu.AddItem(chatLogItem);
            _settingsMenu.AddItem(hidePasswordsItem);
            _settingsMenu.AddItem(autoConnectItem);
            _settingsMenu.AddItem(autoReconnectItem);
            _settingsMenu.AddItem(autoLoginItem);
            _settingsMenu.AddItem(autoRegisterItem);
            _settingsMenu.AddItem(masterItem);
            _settingsMenu.AddItem(debugItem);
            _settingsMenu.AddItem(netGraphItem);

            _mainMenu.RefreshIndex();

            _settingsMenu.RefreshIndex();
            _settingsMenu.SetMenuWidthOffset(100);

            _menuPool.Add(_mainMenu);
            _menuPool.Add(_serverBrowserMenu);
            _menuPool.Add(_settingsMenu);
#endregion

            _debug = new DebugWindow();
            _logger = new Logger();

            DebugLogger = new Debug();
            DebugLogger.Enabled = PlayerSettings.ShowNetGraph;

            UI.Notify("~g~GTA V Coop mod v" + ReadableScriptVersion() + " loaded successfully.~w~");
            if (PlayerSettings.AutoConnect && !String.IsNullOrWhiteSpace(PlayerSettings.LastIP) && PlayerSettings.LastPort != -1 && PlayerSettings.LastPort != 0) { 
                ConnectToServer(PlayerSettings.LastIP.ToString(), PlayerSettings.LastPort);
            }

            _playerList = new PlayerList();
        }

        private void RebuildServerBrowser()
        {
            _serverBrowserMenu.Clear();
            _serverBrowserMenu.RefreshIndex();

            if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
            {
                UI.Notify("No master server has been configured.");
                return;
            }

            string body = null;
            try
            {
                // WebClient? what year is this?
                using(var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (GTA Coop " + ReadableScriptVersion() + ")");
                    body = wc.DownloadString(PlayerSettings.MasterServerAddress);
                }
            }
            catch(Exception e)
            {
                UI.Notify("~r~Error: ~s~Failed to contact master server, please check your configured master server");
                _logger.WriteException("Failed to connect to master " + PlayerSettings.MasterServerAddress, e);

                // backup master server
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (GTA Coop " + ReadableScriptVersion() + ")");
                        body = wc.DownloadString(PlayerSettings.BackupMasterServerAddress);

                        UI.Notify("Backup master server is online, using that one.");
                    }
                }
                catch(Exception ex)
                {
                    UI.Notify("~r~Error: ~s~Failed to contact backup master server.");
                    _logger.WriteException("Failed to connect to master " + PlayerSettings.BackupMasterServerAddress, ex);
                }
            }

            if (body == null)
                return;

            // deserialize master server response

            MasterServerList response;
            try
            {
                response = JsonConvert.DeserializeObject<MasterServerList>(body);
            }
            catch(JsonException e)
            {
                UI.Notify("~r~Error: ~s~Master server returned unusual response. Check error log for details.");
                _logger.WriteException("Failed to parse master server response", e);

                return;
            }

            Console.WriteLine("Servers returned by master server: " + response.List.Count);
            _serverBrowserMenu.Subtitle.Caption = "Servers listed: ~g~~h~" + response.List.Count;
            var item = new UIMenuItem(response.List.Count + " Servers listed.");
            item.SetLeftBadge(UIMenuItem.BadgeStyle.Star);

            if (_client == null)
            {
                var port = GetOpenUdpPort();
                if (port == 0)
                {
                    UI.Notify("No available UDP port was found.");
                    return;
                }
                _config.Port = port;
                _client = new NetClient(_config);
                _client.Start();

                DebugLogger.NetClient = _client;
            }

            foreach (var server in response.List)
            {
                var split = server.Split(':');

                int port;
                // take the last : since IPv6 addresses are like [::1]:4499
                if (!int.TryParse(split[split.Length - 1], out port))
                    continue;
                _client.DiscoverKnownPeer(split[0], port);
            }
        }

        private void DiscoverLan()
        {
            _serverBrowserMenu.Clear();
            _serverBrowserMenu.RefreshIndex();

            if (_client == null)
            {
                var port = GetOpenUdpPort();
                if (port == 0)
                {
                    UI.Notify("No available UDP port was found.");
                    return;
                }
                _config.Port = port;
                _client = new NetClient(_config);
                _client.Start();

                DebugLogger.NetClient = _client;
            }

            _client.DiscoverLocalPeers(4499);
            _serverBrowserMenu.Subtitle.Caption = "Servers on LAN";
        }

        public static Dictionary<int, int> CheckPlayerVehicleMods()
        {
            if (!Game.Player.Character.IsInVehicle()) return null;

            if (_modSwitch % 30 == 0)
            {
                var id = _modSwitch/30;
                var mod = Game.Player.Character.CurrentVehicle.GetMod((VehicleMod) id);
                if (mod != -1)
                {
                    lock (_vehMods)
                    {
                        if (!_vehMods.ContainsKey(id)) _vehMods.Add(id, mod);

                        _vehMods[id] = mod;
                    }
                }
            }

            _modSwitch++;

            if (_modSwitch >= 1500) _modSwitch = 0;

            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch % 30 == 0)
            {
                var id = _pedSwitch / 30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Game.Player.Character.Handle, id);
                if (mod != -1)
                {
                    lock (_pedClothes)
                    {
                        if (!_pedClothes.ContainsKey(id)) _pedClothes.Add(id, mod);

                        _pedClothes[id] = mod;
                    }
                }
            }

            _pedSwitch++;

            if (_pedSwitch >= 450) _pedSwitch = 0;

            return _pedClothes;
        }

        private static int _lastDataSend;
        private static int _tickRate = 60;

        public static void SendPlayerData()
        {
            if (Environment.TickCount - _lastDataSend < 1000 / _tickRate) return;
            _lastDataSend = Environment.TickCount;

            var player = Game.Player.Character;
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.Quaternion = veh.Quaternion.ToLQuaternion();
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PrimaryColor = (int)veh.PrimaryColor;
                obj.SecondaryColor = (int)veh.SecondaryColor;
                obj.PlayerHealth = player.Health;
                obj.VehicleHealth = veh.Health;
                obj.VehicleSeat = Util.GetPedSeat(player);
                obj.VehicleMods = CheckPlayerVehicleMods();
                obj.WheelSpeed = veh.WheelSpeed;
                obj.Steering = veh.SteeringAngle;
                obj.Speed = veh.Speed;
                obj.Plate = veh.NumberPlate;
                obj.Livery = Function.Call<int>(Hash.GET_VEHICLE_LIVERY, veh);

                obj.PedProps = CheckPlayerProps();
                obj.RadioStation = (int)Game.RadioStation;

                if (veh.Model.IsPlane)
                    obj.LandingGearState = veh.LandingGear;

                if (Game.Player.IsPressingHorn)
                    obj.Flag |= (byte)VehicleDataFlags.PressingHorn;

                if (veh.SirenActive)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;

                if (veh.HighBeamsOn)
                    obj.Flag |= (byte)VehicleDataFlags.HighBeamsOn;

                if (veh.LightsOn)
                    obj.Flag |= (byte)VehicleDataFlags.LightsOn;

                if (veh.EngineRunning)
                    obj.Flag |= (byte)VehicleDataFlags.EngineRunning;

                if (veh.IsInBurnout())
                    obj.Flag |= (byte)VehicleDataFlags.InBurnout;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.VehiclePositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = player.IsShooting && player.Weapons.Current?.AmmoInClip != 0;

                GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                if (aiming || shooting)
                    aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation,
                        new Vector2(0, 0));

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Quaternion.ToLQuaternion();
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = player.Health;

                obj.PedProps = CheckPlayerProps();

                if (aiming)
                    obj.Flag |= (byte)PedDataFlags.IsAiming;
                if (shooting)
                    obj.Flag |= (byte)PedDataFlags.IsShooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (byte)PedDataFlags.IsJumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (byte)PedDataFlags.IsParachuteOpen;
                if (Function.Call<bool>(Hash.IS_PED_IN_PARACHUTE_FREE_FALL, player.Handle))
                    obj.Flag |= (byte)PedDataFlags.IsInParachuteFreeFall;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();

                msg.Write((int)PacketType.PedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }

        public static void SendPedData(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                var veh = ped.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.Quaternion = veh.Quaternion.ToLQuaternion();
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedModelHash = ped.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PrimaryColor = (int)veh.PrimaryColor;
                obj.SecondaryColor = (int)veh.SecondaryColor;
                obj.PlayerHealth = ped.Health;
                obj.VehicleHealth = veh.Health;
                obj.VehicleSeat = Util.GetPedSeat(ped);
                obj.Name = ped.Handle.ToString();
                obj.Speed = veh.Speed;
                obj.WheelSpeed = veh.WheelSpeed;
                obj.Steering = veh.SteeringAngle;
                obj.Plate = veh.NumberPlate;
                obj.Livery = Function.Call<int>(Hash.GET_VEHICLE_LIVERY, veh);

                if (veh.Model.IsPlane)
                    obj.LandingGearState = veh.LandingGear;

                if (veh.SirenActive)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;

                if (veh.HighBeamsOn)
                    obj.Flag |= (byte)VehicleDataFlags.HighBeamsOn;

                if (veh.LightsOn)
                    obj.Flag |= (byte)VehicleDataFlags.LightsOn;

                if (veh.EngineRunning)
                    obj.Flag |= (byte)VehicleDataFlags.EngineRunning;

                if (veh.IsInBurnout())
                    obj.Flag |= (byte)VehicleDataFlags.InBurnout;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.NpcVehPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool shooting = ped.IsShooting && ped.Weapons.Current?.AmmoInClip != 0;

                GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                if (shooting)
                    aimCoord = Util.GetLastWeaponImpact(ped);

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = ped.Position.ToLVector();
                obj.Quaternion = ped.Quaternion.ToLQuaternion();
                obj.PedModelHash = ped.Model.Hash;
                obj.WeaponHash = (int)ped.Weapons.Current.Hash;
                obj.PlayerHealth = ped.Health;
                obj.Name = ped.Handle.ToString();

                if (shooting)
                    obj.Flag |= (byte)PedDataFlags.IsShooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, ped.Handle))
                    obj.Flag |= (byte)PedDataFlags.IsJumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, ped.Handle) == 2)
                    obj.Flag |= (byte)PedDataFlags.IsParachuteOpen;
                if (Function.Call<bool>(Hash.IS_PED_IN_PARACHUTE_FREE_FALL, ped.Handle))
                    obj.Flag |= (byte)PedDataFlags.IsInParachuteFreeFall;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();

                msg.Write((int)PacketType.NpcPedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }

#if VOICE
        void SendVoiceData(object sender, WaveInEventArgs e)
        {
            if (!IsOnServer())
                return;

            if (_isTalking)
            {
                var msg = _client.CreateMessage();

                msg.Write((int) PacketType.VoiceChatData);

                msg.Write(e.Buffer.Length);
                msg.Write(e.Buffer);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);
            }
        }
#endif

        public void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;
                _menuPool.ProcessMenus();
                _chat.Tick();
                _playerList.Tick(!_menuPool.IsAnyMenuOpen());

                if (_isGoingToCar && Game.IsControlJustPressed(0, Control.PhoneCancel))
                {
                    Game.Player.Character.Task.ClearAll();
                    _isGoingToCar = false;
                }

                if (IsOnServer())
                {
                    if (!_mainMenu.MenuItems[2].Text.Equals("Disconnect"))
                    {
                        _mainMenu.MenuItems[2].Text = "Disconnect";
                    }
                    if (_settingsMenu.MenuItems[0].Enabled)
                    {
                        _settingsMenu.MenuItems[0].Enabled = false;
                    }
                }
                else
                {
                    if (_mainMenu.MenuItems[2].Text.Equals("Disconnect"))
                    {
                        _mainMenu.MenuItems[2].Text = "Connect";
                    }
                    if (!_settingsMenu.MenuItems[0].Enabled)
                    {
                        _settingsMenu.MenuItems[0].Enabled = true;
                    }
                }

                if (debug)
                {
                    Debug();
                }
                ProcessMessages();
                DebugLogger.Tick();

                if (_client == null || _client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                    _client.ConnectionStatus == NetConnectionStatus.None) return;

                if (_wasTyping)
                    Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);

                int time = 1000;
                if ((time = Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH)) < 50 && !_lastDead)
                {
                    _lastDead = true;
                    var msg = _client.CreateMessage();
                    msg.Write((int)PacketType.PlayerKilled);
                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
                }

                if (time > 50 && _lastDead)
                    _lastDead = false;

                if (!PlayerSettings.SyncWorld)
                {
                    Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                    Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                }
                switch (PlayerSettings.SyncTraffic)
                {
                    case TrafficMode.Parked:
                        Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        break;
                    case TrafficMode.None:
                        Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        break;
                    case TrafficMode.All:
                        break;
                    default:
                        break;
                }
                if(PlayerSettings.SyncWorld || PlayerSettings.SyncTraffic != TrafficMode.All)
                {
                    Function.Call((Hash)0x2F9A292AD0A3BD89);
                    Function.Call((Hash)0x5F3B7749C112D552);
                }
                Function.Call(Hash.SET_TIME_SCALE, 1f);

                /*string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                    _bytesSent / 1000, _messagesReceived, _messagesSent);
                    */
                //UI.ShowSubtitle(stats);

                lock (_threadJumping)
                {
                    if (_threadJumping.Any())
                    {
                        Action action = _threadJumping.Dequeue();
                        if (action != null) action.Invoke();
                    }
                }

#if VOICE
                // push to talk
                if (Game.IsControlPressed(0, Control.PushToTalk))
                    _isTalking = true;
                else
                    _isTalking = false;
#endif

                Dictionary<string, NativeData> tickNatives = null;
                lock (_tickNatives) tickNatives = new Dictionary<string, NativeData>(_tickNatives);

                for (int i = 0; i < tickNatives.Count; i++) DecodeNativeCall(tickNatives.ElementAt(i).Value);
            }catch(Exception ex)
            {
                UI.Notify("<ERROR> Could not handle this tick: " + ex.ToString());
                _logger.WriteException("Could not handle tick", ex);
            }
        }
		
        private void OnShutdown(object sender, EventArgs e)
        {
            if (IsOnServer())
                _client.Disconnect("Connection closed by peer.");

#if VOICE
            if (_waveOutput != null)
            {
                _waveOutput.Stop();
                _waveOutput.Dispose();
            }

            if (_waveInput != null)
            {
                _waveInput.StopRecording();
                _waveInput.Dispose();
            }

            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
            }
#endif
        }

        public static bool IsOnServer()
        {
            return _client != null && _client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            _chat.OnKeyDown(e.KeyCode);
            if (e.KeyCode == PlayerSettings.ActivationKey && !_chat.IsFocused)
            {
                if (_menuPool.IsAnyMenuOpen())
                {
                    _menuPool.CloseAllMenus();
                }
                else
                {
                    _mainMenu.Visible = true;
                }
            }

            if (e.KeyCode == Keys.G && !Game.Player.Character.IsInVehicle() && IsOnServer() && !_chat.IsFocused)
            {
                var vehs = World.GetAllVehicles().OrderBy(v => (v.Position - Game.Player.Character.Position).Length()).Take(1).ToList();
                if (vehs.Any() && Game.Player.Character.IsInRangeOf(vehs[0].Position, 5f))
                {
                    Game.Player.Character.Task.EnterVehicle(vehs[0], (VehicleSeat)Util.GetFreePassengerSeat(vehs[0]));
                    _isGoingToCar = true;
                }
            }

            if (Game.IsControlPressed(0, Control.MpTextChatAll) && IsOnServer())
            {
                _chat.IsFocused = true;
                _wasTyping = true;
            }

            if (Game.IsControlJustPressed(0, Control.MultiplayerInfo) && IsOnServer() && !_chat.IsFocused)
            {
                var time = Function.Call<long>(Hash.GET_GAME_TIMER);

                if ((time - _playerList.Pressed) < 5000)
                {
                    // still being drawn
                    _playerList.Pressed = time - 6000;
                }
                else
                {
                    _playerList.Pressed = time;
                }
            }
        }

        public void ConnectToServer(string ip, int port = 4499)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            _chat.Init();

            if (_client == null)
            {
                var cport = GetOpenUdpPort();
                if (cport == 0)
                {
                    UI.Notify("No available UDP port was found.");
                    return;
                }
                _config.Port = cport;
                _client = new NetClient(_config);
                _client.Start();

                DebugLogger.NetClient = _client;
            }

            lock (Opponents) Opponents = new Dictionary<long, SyncPed>();
            lock (Npcs) Npcs = new Dictionary<string, SyncPed>();
            lock (_tickNatives) _tickNatives = new Dictionary<string, NativeData>();

            var msg = _client.CreateMessage();

            var obj = new ConnectionRequest();
            obj.Name = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Player" : Game.Player.Name; // To be used as identifiers in server files
            obj.DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.Username) ? obj.Name : PlayerSettings.Username.Trim();
            if (!string.IsNullOrEmpty(_password)) obj.Password = _password;
            obj.ScriptVersion = (byte)LocalScriptVersion;
            obj.GameVersion = (int)Game.Version;

            var bin = SerializeBinary(obj);

            msg.Write((int)PacketType.ConnectionRequest);
            msg.Write(bin.Length);
            msg.Write(bin);

            _client.Connect(ip, port == 0 ? Port : port, msg);

            var pos = Game.Player.Character.Position;
            Function.Call(Hash.CLEAR_AREA_OF_PEDS, pos.X, pos.Y, pos.Z, 100f, 0);
            Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 100f, 0);

            Function.Call(Hash.SET_GARBAGE_TRUCKS, 0);
            Function.Call(Hash.SET_RANDOM_BOATS, 0);
            Function.Call(Hash.SET_RANDOM_TRAINS, 0);
            _lastIP = ip; _lastPort = port;
        }

        public void ProcessMessages()
        {
            NetIncomingMessage msg;
            while (_client != null && (msg = _client.ReadMessage()) != null)
            {
                _messagesReceived++;
                _bytesReceived += msg.LengthBytes;

                if (msg.MessageType == NetIncomingMessageType.Data)
                {
                    var type = (PacketType)msg.ReadInt32();
                    switch (type)
                    {
#if VOICE
                        case PacketType.VoiceChatData:
                            {
                                var user = msg.ReadInt32();

                                var len = msg.ReadInt32();
                                var data = msg.ReadBytes(len);
                            
                                UI.ShowSubtitle(len + " ");

                                _playBuffer.AddSamples(data, 0, data.Length);
                            }
                            break;
#endif
                        case PacketType.VehiclePositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;

                                if (data == null) return;

                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion());
                                        Opponents.Add(data.Id, repr);

                                        Opponents[data.Id].Name = data.Name;
                                        Opponents[data.Id].Latency = data.Latency;

                                        _playerList.Update(Opponents);
                                    }

                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].LastUpdateReceived = Environment.TickCount;
                                    Opponents[data.Id].VehiclePosition =
                                        data.Position.ToVector();
                                    Opponents[data.Id].VehicleVelocity = data.Velocity.ToVector();
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].VehicleHash =
                                        data.VehicleModelHash;
                                    Opponents[data.Id].VehicleRotation =
                                        data.Quaternion.ToQuaternion();
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].VehicleHealth = data.VehicleHealth;
                                    Opponents[data.Id].VehiclePrimaryColor = data.PrimaryColor;
                                    Opponents[data.Id].VehicleSecondaryColor = data.SecondaryColor;
                                    Opponents[data.Id].VehicleSeat = data.VehicleSeat;
                                    Opponents[data.Id].IsInVehicle = true;
                                    Opponents[data.Id].Latency = data.Latency;
                                    Opponents[data.Id].VehicleMods = data.VehicleMods;

                                    Opponents[data.Id].IsHornPressed = (data.Flag & (byte)VehicleDataFlags.PressingHorn) > 0;
                                    Opponents[data.Id].Siren = (data.Flag & (byte)VehicleDataFlags.SirenActive) > 0;
                                    Opponents[data.Id].IsInBurnout = (data.Flag & (short)VehicleDataFlags.InBurnout) > 0;
                                    Opponents[data.Id].HighBeamsOn = (data.Flag & (short)VehicleDataFlags.HighBeamsOn) > 0;
                                    Opponents[data.Id].LightsOn = (data.Flag & (short)VehicleDataFlags.LightsOn) > 0;
                                    Opponents[data.Id].IsEngineRunning = (data.Flag & (short)VehicleDataFlags.EngineRunning) > 0;

                                    Opponents[data.Id].Speed = data.Speed;
                                    Opponents[data.Id].WheelSpeed = data.WheelSpeed;
                                    Opponents[data.Id].Steering = data.Steering;
                                    Opponents[data.Id].RadioStation = data.RadioStation;
                                    Opponents[data.Id].Plate = data.Plate;
                                    Opponents[data.Id].LandingGear = data.LandingGearState;
                                    Opponents[data.Id].Livery = data.Livery;
                                    Opponents[data.Id].PedProps = data.PedProps;
                                }
                            }
                            break;
                        case PacketType.PedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion());
                                        Opponents.Add(data.Id, repr);

                                        Opponents[data.Id].Name = data.Name;
                                        Opponents[data.Id].Latency = data.Latency;

                                        _playerList.Update(Opponents);
                                    }

                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].LastUpdateReceived = Environment.TickCount;
                                    Opponents[data.Id].Position = data.Position.ToVector();
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].Rotation = data.Quaternion.ToQuaternion();
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].IsInVehicle = false;
                                    Opponents[data.Id].AimCoords = data.AimCoords.ToVector();
                                    Opponents[data.Id].CurrentWeapon = data.WeaponHash;
                                    Opponents[data.Id].IsAiming = (data.Flag & (byte)PedDataFlags.IsAiming) > 0;
                                    Opponents[data.Id].IsJumping = (data.Flag & (byte)PedDataFlags.IsJumping) > 0;
                                    Opponents[data.Id].IsShooting = (data.Flag & (byte)PedDataFlags.IsShooting) > 0;
                                    Opponents[data.Id].Latency = data.Latency;
                                    Opponents[data.Id].IsParachuteOpen = (data.Flag & (byte)PedDataFlags.IsParachuteOpen) > 0;
                                    Opponents[data.Id].IsInParachuteFreeFall = (data.Flag & (byte)PedDataFlags.IsInParachuteFreeFall) > 0;
                                    Opponents[data.Id].PedProps = data.PedProps;
                                }
                            }
                            break;
                        case PacketType.NpcVehPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;

                                lock (Npcs)
                                {
                                    if (!Npcs.ContainsKey(data.Name))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion(), false);
                                        Npcs.Add(data.Name, repr);
                                        Npcs[data.Name].Name = "";
                                        Npcs[data.Name].Host = data.Id;
                                    }

                                    Npcs[data.Name].LastUpdateReceived = Environment.TickCount;
                                    Npcs[data.Name].VehiclePosition =
                                        data.Position.ToVector();
                                    Npcs[data.Name].VehicleVelocity = data.Velocity.ToVector();
                                    Npcs[data.Name].ModelHash = data.PedModelHash;
                                    Npcs[data.Name].VehicleHash =
                                        data.VehicleModelHash;
                                    Npcs[data.Name].VehicleRotation =
                                        data.Quaternion.ToQuaternion();
                                    Npcs[data.Name].PedHealth = data.PlayerHealth;
                                    Npcs[data.Name].VehicleHealth = data.VehicleHealth;
                                    Npcs[data.Name].VehiclePrimaryColor = data.PrimaryColor;
                                    Npcs[data.Name].VehicleSecondaryColor = data.SecondaryColor;
                                    Npcs[data.Name].VehicleSeat = data.VehicleSeat;
                                    Npcs[data.Name].IsInVehicle = true;

                                    Npcs[data.Name].IsHornPressed = (data.Flag & (byte)VehicleDataFlags.PressingHorn) > 0;
                                    Npcs[data.Name].Siren = (data.Flag & (byte)VehicleDataFlags.SirenActive) > 0;
                                    Npcs[data.Name].IsInBurnout = (data.Flag & (short)VehicleDataFlags.InBurnout) > 0;
                                    Npcs[data.Name].HighBeamsOn = (data.Flag & (short)VehicleDataFlags.HighBeamsOn) > 0;
                                    Npcs[data.Name].LightsOn = (data.Flag & (short)VehicleDataFlags.LightsOn) > 0;
                                    Npcs[data.Name].IsEngineRunning = (data.Flag & (short)VehicleDataFlags.EngineRunning) > 0;

                                    Npcs[data.Name].Speed = data.Speed;
                                    Npcs[data.Name].WheelSpeed = data.WheelSpeed;
                                    Npcs[data.Name].Steering = data.Steering;
                                    Npcs[data.Name].RadioStation = data.RadioStation;
                                    Npcs[data.Name].Plate = data.Plate;
                                    Npcs[data.Name].LandingGear = data.LandingGearState;
                                    Npcs[data.Name].Livery = data.Livery;
                                    Npcs[data.Name].PedProps = data.PedProps;
                                }
                            }
                            break;
                        case PacketType.NpcPedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                lock (Npcs)
                                {
                                    if (!Npcs.ContainsKey(data.Name))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion(), false);
                                        Npcs.Add(data.Name, repr);
                                        Npcs[data.Name].Name = "";
                                        Npcs[data.Name].Host = data.Id;
                                    }

                                    Npcs[data.Name].LastUpdateReceived = Environment.TickCount;
                                    Npcs[data.Name].Position = data.Position.ToVector();
                                    Npcs[data.Name].ModelHash = data.PedModelHash;
                                    Npcs[data.Name].Rotation = data.Quaternion.ToQuaternion();
                                    Npcs[data.Name].PedHealth = data.PlayerHealth;
                                    Npcs[data.Name].IsInVehicle = false;
                                    Npcs[data.Name].AimCoords = data.AimCoords.ToVector();
                                    Npcs[data.Name].CurrentWeapon = data.WeaponHash;
                                    Npcs[data.Name].IsAiming = (data.Flag & (byte)PedDataFlags.IsAiming) > 0;
                                    Npcs[data.Name].IsJumping = (data.Flag & (byte)PedDataFlags.IsJumping) > 0;
                                    Npcs[data.Name].IsShooting = (data.Flag & (byte)PedDataFlags.IsShooting) > 0;
                                    Npcs[data.Name].IsParachuteOpen = (data.Flag & (byte)PedDataFlags.IsParachuteOpen) > 0;
                                    Npcs[data.Name].IsInParachuteFreeFall = (data.Flag & (byte)PedDataFlags.IsInParachuteFreeFall) > 0;
                                }
                            }
                            break;
                        case PacketType.ChatData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                if (data != null && !string.IsNullOrEmpty(data.Message))
                                {
                                    var sender = string.IsNullOrEmpty(data.Sender) ? "SERVER" : data.Sender;
                                    if (data.Message.ToString().Equals("Welcome back, use /login (password) to login to your account"))
                                    {
                                        if (!String.IsNullOrWhiteSpace(PlayerSettings.AutoLogin))
                                        {
                                            var obj = new ChatData()
                                            {
                                                Message = "/login " + PlayerSettings.AutoLogin,
                                            };
                                            var Data = SerializeBinary(obj);

                                            var Msg = _client.CreateMessage();
                                            Msg.Write((int)PacketType.ChatData);
                                            Msg.Write(Data.Length);
                                            Msg.Write(Data);
                                            _client.SendMessage(Msg, NetDeliveryMethod.ReliableOrdered, 0);
                                            return;
                                        }
                                    }
                                    if (data.Message.ToString().Equals("You can register an account using /register (password)"))
                                    {
                                        if (!String.IsNullOrWhiteSpace(PlayerSettings.AutoLogin) && PlayerSettings.AutoRegister)
                                        {
                                            var obj = new ChatData()
                                            {
                                                Message = "/register " + PlayerSettings.AutoLogin,
                                            };
                                            var Data = SerializeBinary(obj);

                                            var Msg = _client.CreateMessage();
                                            Msg.Write((int)PacketType.ChatData);
                                            Msg.Write(Data.Length);
                                            Msg.Write(Data);
                                            _client.SendMessage(Msg, NetDeliveryMethod.ReliableOrdered, 0);
                                            return;
                                        }
                                    }
                                    _chat.AddMessage(sender, data.Message);
                                    /*lock (_threadJumping)
                                    {
                                        _threadJumping.Enqueue(() =>
                                        {
                                            if (!string.IsNullOrEmpty(data.Sender))
                                                for (int i = 0; i < data.Message.Length; i += 97 - data.Sender.Length)
                                                {
                                                    UI.Notify(data.Sender + ": " +
                                                              data.Message.Substring(i,
                                                                  Math.Min(97 - data.Sender.Length,
                                                                      data.Message.Length - i)));
                                                }
                                            else
                                                for (int i = 0; i < data.Message.Length; i += 99)
                                                    UI.Notify(data.Message.Substring(i,
                                                        Math.Min(99, data.Message.Length - i)));
                                        });
                                    }*/
                                }
                            }
                            break;
                        case PacketType.PlayerDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                lock (Opponents)
                                {
                                    if (data != null && Opponents.ContainsKey(data.Id))
                                    {
                                        Opponents[data.Id].Clear();
                                        Opponents.Remove(data.Id);

                                        lock (Npcs)
                                        {
                                            foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id))
                                            {
                                                pair.Value.Clear();
                                                Npcs.Remove(pair.Key);
                                            }
                                        }

                                        _playerList.Update(Opponents);
                                    }
                                }
                            }
                            break;
                        case PacketType.WorldSharingStop:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                if (data == null) return;
                                lock (Npcs)
                                {
                                    foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id).ToList())
                                    {
                                        pair.Value.Clear();
                                        Npcs.Remove(pair.Key);
                                    }
                                }
                            }
                            break;
                        case PacketType.NativeCall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                DecodeNativeCall(data);
                            }
                            break;
                        case PacketType.NativeTick:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_tickNatives)
                                {
                                    if (!_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Add(data.Identifier, data.Native);

                                    _tickNatives[data.Identifier] = data.Native;
                                }
                            }
                            break;
                        case PacketType.NativeTickRecall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_tickNatives) if (_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Remove(data.Identifier);
                            }
                            break;
                        case PacketType.NativeOnDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_dcNatives)
                                {
                                    if (!_dcNatives.ContainsKey(data.Id)) _dcNatives.Add(data.Id, data);
                                    _dcNatives[data.Id] = data;
                                }
                            }
                            break;
                        case PacketType.NativeOnDisconnectRecall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_dcNatives) if (_dcNatives.ContainsKey(data.Id)) _dcNatives.Remove(data.Id);
                            }
                            break;
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.ConnectionLatencyUpdated)
                {
                    Latency = msg.ReadFloat();
                }
                else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    var newStatus = (NetConnectionStatus)msg.ReadByte();
                    switch (newStatus)
                    {
                        case NetConnectionStatus.InitiatedConnect:
                            UI.Notify("Connecting...");
                            UpdateStatusText("Connecting to server");

                            break;
                        case NetConnectionStatus.Connected:
                            UI.Notify("Connection successful!");
                            UpdateStatusText(null);

                            Util.DisplayHelpText("Press ~INPUT_MULTIPLAYER_INFO~ to view a list of online players");
#if VOICE
                            _waveInput.StartRecording();
#endif

                            // close F9 menu when connected
                            if (_menuPool.IsAnyMenuOpen())
                            {
                                _menuPool.CloseAllMenus();
                            }

                            _channel = msg.SenderConnection.RemoteHailMessage.ReadInt32();

                            break;
                        case NetConnectionStatus.Disconnected:
                            var reason = msg.ReadString();
                            UI.Notify("You have been disconnected" + (string.IsNullOrEmpty(reason) ? " from the server." : ": " + reason));
                            UpdateStatusText(null);

#if VOICE
                            _waveInput.StopRecording();
#endif

                            lock (Opponents)
                            {
                                if (Opponents != null)
                                {
                                    Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                    Opponents.Clear();
                                }
                            }

                            lock (Npcs)
                            {
                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }
                            }

                            lock (_dcNatives)
                                if (_dcNatives != null && _dcNatives.Any())
                                {
                                    _dcNatives.ToList().ForEach(pair => DecodeNativeCall(pair.Value));
                                    _dcNatives.Clear();
                                }

                            lock (_tickNatives) if (_tickNatives != null) _tickNatives.Clear();

                            lock (_entityCleanup)
                            {
                                _entityCleanup.ForEach(ent => new Prop(ent).Delete());
                                _entityCleanup.Clear();
                            }
                            lock (_blipCleanup)
                            {
                                _blipCleanup.ForEach(blip => new Blip(blip).Remove());
                                _blipCleanup.Clear();
                            }
                            //if (PlayerSettings.AutoReconnect && !reason.StartsWith("KICKED: ") && !reason.Equals("Connection closed by peer.") && !reason.Equals("Stopping Server") && !reason.Equals("Switching servers."))
                            if (PlayerSettings.AutoConnect && (reason.Equals("Connection timed out") || reason.StartsWith("Could not connect to remote host:")))
                            {
                                ConnectToServer(_lastIP, _lastPort);
                            }

                            // clear chat
                            _chat.Reset();

                            break;
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.DiscoveryResponse)
                {
                    var type = msg.ReadInt32();
                    var len = msg.ReadInt32();
                    var bin = msg.ReadBytes(len);
                    var data = DeserializeBinary<DiscoveryResponse>(bin) as DiscoveryResponse;

                    if (data == null) return;

                    var database = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "geoip.mmdb");
                    string description = msg.SenderEndPoint.Address.ToString() + ":" + data.Port;

                    try
                    {
                        using (var reader = new DatabaseReader(database))
                        {
                            var country = reader.Country(msg.SenderEndPoint.Address.ToString());
                            description = $"[{country.Continent.Code}/{country.Country.IsoCode}] " + description;
                        }
                    }
                    catch(AddressNotFoundException)
                    {
                    }
                    catch (Exception e)
                    {
                        UI.Notify("GeoIP2 failed, check error log for details.");
                        _logger.WriteException("Failed GeoIP2", e);
                    }

                    var item = new UIMenuItem(data.ServerName);

                    TextInfo textinfo = new CultureInfo("en-US", false).TextInfo;
                    string title = textinfo.ToTitleCase(data.Gamemode.ToString());

                    var gamemode = data.Gamemode == null ? "Unknown" : title;

                    item.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);
                    item.Description = description;

                    if (data.PasswordProtected)
                        item.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                    int lastIndx = 0;
                    if (_serverBrowserMenu.Size > 0)
                        lastIndx = _serverBrowserMenu.CurrentSelection;

                    var gMsg = msg;
                    item.Activated += (sender, selectedItem) =>
                    {
                        if (IsOnServer())
                        {
                            _client.Disconnect("Switching servers.");

                            if (Opponents != null)
                            {
                                Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                Opponents.Clear();
                            }

                            if (Npcs != null)
                            {
                                Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                Npcs.Clear();
                            }

                            while (IsOnServer()) Script.Yield();
                        }

                        if (data.PasswordProtected)
                        {
                            _password = Game.GetUserInput(256);
                            _passItem.SetRightLabel(_password);
                        }
                        ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port);
                        _serverBrowserMenu.Visible = false;
                    };

                    _serverBrowserMenu.AddItem(item);
                    _serverBrowserMenu.CurrentSelection = lastIndx;
                    //_serverBrowserMenu.Subtitle.Caption = "Servers listed: ~g~~h~" + dejson.list.Count().ToString();
                }
            }
        }

#region debug stuff

        private DateTime _artificialLagCounter = DateTime.MinValue;
        private bool _debugStarted;
        private SyncPed _debugSyncPed;

        private void Debug()
        {

            _debug.Visible = true;
            _debug.Draw();

#if DEBUGSYNCPED
            var player = Game.Player.Character;
            if (_debugSyncPed == null)
            {
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Rotation, false);
            }

            if (DateTime.Now.Subtract(_artificialLagCounter).TotalMilliseconds >= 300)
            {
                _artificialLagCounter = DateTime.Now;
                if (player.IsInVehicle())
                {
                    var veh = player.CurrentVehicle;

                    _debugSyncPed.VehiclePosition = veh.Position;
                    _debugSyncPed.VehicleRotation = veh.Rotation;
                    _debugSyncPed.VehicleVelocity = veh.Velocity;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.VehicleHash = veh.Model.Hash;
                    _debugSyncPed.VehiclePrimaryColor = (int)veh.PrimaryColor;
                    _debugSyncPed.VehicleSecondaryColor = (int)veh.SecondaryColor;
                    _debugSyncPed.PedHealth = player.Health;
                    _debugSyncPed.VehicleHealth = veh.Health;
                    _debugSyncPed.VehicleSeat = Util.GetPedSeat(player);
                    _debugSyncPed.IsHornPressed = Game.Player.IsPressingHorn;
                    _debugSyncPed.Siren = veh.SirenActive;
                    _debugSyncPed.VehicleMods = CheckPlayerVehicleMods();
                    _debugSyncPed.Speed = veh.Speed;
                    _debugSyncPed.Steering = veh.SteeringAngle;
                    _debugSyncPed.IsInVehicle = true;
                    _debugSyncPed.PedProps = CheckPlayerProps();
                    _debugSyncPed.LastUpdateReceived = Environment.TickCount;
                }
                else
                {
                    bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                    bool shooting = player.IsShooting && player.Weapons.Current?.AmmoInClip != 0;

                    GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                    if (aiming || shooting)
                        aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation,
                            new Vector2(0, 0));

                    _debugSyncPed.AimCoords = aimCoord;
                    _debugSyncPed.Position = player.Position;
                    _debugSyncPed.Rotation = player.Rotation;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.CurrentWeapon = (int)player.Weapons.Current.Hash;
                    _debugSyncPed.PedHealth = player.Health;
                    _debugSyncPed.IsAiming = aiming;
                    _debugSyncPed.IsShooting = shooting;
                    _debugSyncPed.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);
                    _debugSyncPed.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2;
                    _debugSyncPed.IsInVehicle = false;
                    _debugSyncPed.PedProps = CheckPlayerProps();
                    _debugSyncPed.LastUpdateReceived = Environment.TickCount;
                }
            }

            _debugSyncPed.DisplayLocally();

            if (_debugSyncPed.Character != null)
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.Character.Handle, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, _debugSyncPed.Character.Handle, false);
            }


            if (_debugSyncPed.MainVehicle != null && player.IsInVehicle())
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.MainVehicle.Handle, player.CurrentVehicle.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle, _debugSyncPed.MainVehicle.Handle, false);
            }
#endif
        }

#endregion

        public void DecodeNativeCall(NativeData obj)
        {
            if(IsNativeBlocked(obj.Hash))
            {
                SendNativeCallResponse(obj.Id, "Native execution of this native blocked by client");
                return;
            }

            var list = new List<InputArgument>();

            foreach (var arg in obj.Arguments)
            {
                if (arg is IntArgument)
                {
                    list.Add(new InputArgument(((IntArgument)arg).Data));
                }
                else if (arg is UIntArgument)
                {
                    list.Add(new InputArgument(((UIntArgument)arg).Data));
                }
                else if (arg is StringArgument)
                {
                    list.Add(new InputArgument(((StringArgument)arg).Data));
                }
                else if (arg is FloatArgument)
                {
                    list.Add(new InputArgument(((FloatArgument)arg).Data));
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(new InputArgument(((BooleanArgument)arg).Data));
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(new InputArgument(Game.Player.Character.Handle));
                }
                else if (arg is OpponentPedHandleArgument)
                {
                    var handle = ((OpponentPedHandleArgument)arg).Data;
                    lock (Opponents) if (Opponents.ContainsKey(handle) && Opponents[handle].Character != null) list.Add(new InputArgument(Opponents[handle].Character.Handle));
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new InputArgument(tmp.X));
                    list.Add(new InputArgument(tmp.Y));
                    list.Add(new InputArgument(tmp.Z));
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(new InputArgument(Game.Player.Handle));
                }
            }

            var nativeType = CheckNativeHash(obj.Hash);

            if ((int)nativeType >= 2)
            {
                if ((int) nativeType >= 3)
                {
                    var modelObj = obj.Arguments[(int) nativeType - 3];
                    int modelHash = 0;

                    if (modelObj is UIntArgument)
                    {
                        modelHash = unchecked((int) ((UIntArgument) modelObj).Data);
                    }
                    else if (modelObj is IntArgument)
                    {
                        modelHash = ((IntArgument) modelObj).Data;
                    }
                    var model = new Model(modelHash);

                    if (model.IsValid)
                    {
                        model.Request(10000);
                    }
                }

                var entId = Function.Call<int>((Hash) obj.Hash, list.ToArray());
                lock(_entityCleanup) _entityCleanup.Add(entId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, entId);
                }
                return;
            }

            if (nativeType == NativeType.ReturnsBlip)
            {
                var blipId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (_blipCleanup) _blipCleanup.Add(blipId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, blipId);
                }
                return;
            }

            if (obj.ReturnType == null)
            {
                Function.Call((Hash)obj.Hash, list.ToArray());
            }
            else
            {
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<int>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is UIntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<uint>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is StringArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<string>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is FloatArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<float>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is BooleanArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<bool>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is Vector3Argument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<GTA.Math.Vector3>((Hash)obj.Hash, list.ToArray()));
                }
            }
        }

        public void SendNativeCallResponse(string id, object response)
        {
            var obj = new NativeResponse();
            obj.Id = id;

            if (response is int)
            {
                obj.Response = new IntArgument() { Data = ((int)response) };
            }
            else if (response is uint)
            {
                obj.Response = new UIntArgument() { Data = ((uint)response) };
            }
            else if (response is string)
            {
                obj.Response = new StringArgument() { Data = ((string)response) };
            }
            else if (response is float)
            {
                obj.Response = new FloatArgument() { Data = ((float)response) };
            }
            else if (response is bool)
            {
                obj.Response = new BooleanArgument() { Data = ((bool)response) };
            }
            else if (response is GTA.Math.Vector3)
            {
                var tmp = (GTA.Math.Vector3)response;
                obj.Response = new Vector3Argument()
                {
                    X = tmp.X,
                    Y = tmp.Y,
                    Z = tmp.Z,
                };
            }

            var msg = _client.CreateMessage();
            var bin = SerializeBinary(obj);
            msg.Write((int)PacketType.NativeResponse);
            msg.Write(bin.Length);
            msg.Write(bin);
            _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                case 0xD49F9B0955C367DE:
                    return NativeType.ReturnsEntityNeedsModel2;
                case 0x7DD959874C1FD534:
                    return NativeType.ReturnsEntityNeedsModel3;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return NativeType.ReturnsEntityNeedsModel1;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return NativeType.ReturnsEntity;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
            }
        }

        private bool IsNativeBlocked(ulong hash)
        {
            switch(hash)
            {
                default:
                    return false;
                case 0x213AEB2B90CBA7AC: // _COPY_MEMORY
                case 0x5A5F40FE637EB584: // STRING_TO_INT
                case 0xE80492A9AC099A93: // CLEAR_BIT
                case 0x8EF07E15701D61ED: // SET_BITS_IN_RANGE
                case 0x933D6A9EEC1BACD0: // SET_BIT
                    return true;
            }
        }

        public static int GetPedSpeed(GTA.Math.Vector3 firstVector, GTA.Math.Vector3 secondVector)
        {
            float speed = (firstVector - secondVector).Length();
            if (speed < 0.02f)
            {
                return 0;
            }
            else if (speed >= 0.02f && speed < 0.05f)
            {
                return 1;
            }
            else if (speed >= 0.05f && speed < 0.12f)
            {
                return 2;
            }
            else if (speed >= 0.12f)
                return 3;
            return 0;
        }

        public static bool WorldToScreenRel(GTA.Math.Vector3 worldCoords, out Vector2 screenCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                screenCoords = new Vector2();
                return false;
            }
            screenCoords = new Vector2((num1.GetResult<float>() - 0.5f) * 2, (num2.GetResult<float>() - 0.5f) * 2);
            return true;
        }

        public static GTA.Math.Vector3 ScreenRelToWorld(GTA.Math.Vector3 camPos, GTA.Math.Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new GTA.Math.Vector3(10, 0, 0);
            var rotDown = camRot + new GTA.Math.Vector3(-10, 0, 0);
            var rotLeft = camRot + new GTA.Math.Vector3(0, 0, -10);
            var rotRight = camRot + new GTA.Math.Vector3(0, 0, 10);

            var camRight = RotationToDirection(rotRight) - RotationToDirection(rotLeft);
            var camUp = RotationToDirection(rotUp) - RotationToDirection(rotDown);

            var rollRad = -DegToRad(camRot.Y);

            var camRightRoll = camRight * (float)Math.Cos(rollRad) - camUp * (float)Math.Sin(rollRad);
            var camUpRoll = camRight * (float)Math.Sin(rollRad) + camUp * (float)Math.Cos(rollRad);

            var point3D = camPos + camForward * 10.0f + camRightRoll + camUpRoll;
            Vector2 point2D;
            if (!WorldToScreenRel(point3D, out point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            Vector2 point2DZero;
            if (!WorldToScreenRel(point3DZero, out point2DZero)) return camPos + camForward * 10.0f;

            const double eps = 0.001;
            if (Math.Abs(point2D.X - point2DZero.X) < eps || Math.Abs(point2D.Y - point2DZero.Y) < eps) return camPos + camForward * 10.0f;
            var scaleX = (coord.X - point2DZero.X) / (point2D.X - point2DZero.X);
            var scaleY = (coord.Y - point2DZero.Y) / (point2D.Y - point2DZero.Y);
            var point3Dret = camPos + camForward * 10.0f + camRightRoll * scaleX + camUpRoll * scaleY;
            return point3Dret;
        }

        public static GTA.Math.Vector3 RotationToDirection(GTA.Math.Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new GTA.Math.Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static GTA.Math.Vector3 DirectionToRotation(GTA.Math.Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new GTA.Math.Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double DegToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public static double RadToDeg(double deg)
        {
            return deg * 180.0 / Math.PI;
        }

        public static double BoundRotationDeg(double angleDeg)
        {
            var twoPi = (int)(angleDeg / 360);
            var res = angleDeg - twoPi * 360;
            if (res < 0) res += 360;
            return res;
        }

        public static GTA.Math.Vector3 RaycastEverything(Vector2 screenCoord)
        {
            var camPos = GameplayCamera.Position;
            var camRot = GameplayCamera.Rotation;
            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Entity ignoreEntity = Game.Player.Character;
            if (Game.Player.Character.IsInVehicle())
            {
                ignoreEntity = Game.Player.Character.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHitAnything)
            {
                return raycastResults.HitCoords;
            }

            return camPos + dir * raycastToDist;
        }

        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException)
                {
                    return null;
                }
            }
            return output;
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public int GetOpenUdpPort()
        {
            var startingAtPort = 5000;
            var maxNumberOfPortsToCheck = 500;
            var range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            var portsInUse =
                from p in range
                join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            on p equals used.Port
                select p;

            return range.Except(portsInUse).FirstOrDefault();
        }

#if VOICE
        public static List<Object> GetInputDevices()
        {
            var list = new List<Object>();

            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                list.Add(capabilities.ProductName);
            }
            return list;
        }
#endif

        public void UpdateStatusText(string text)
        {
            if(text == null)
            {
                Util.HideBusySpinner();
                return;
            }

            Util.ShowBusySpinner(text);
        }
    }

    public class MasterServerList
    {
        [JsonProperty("list")]
        public List<string> List { get; set; }
    }
}
