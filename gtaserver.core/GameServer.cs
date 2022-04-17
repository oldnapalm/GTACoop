using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Attributes;
using GTAServer.ProtocolMessages;
using Lidgren.Network;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI.Events;
using GTAServer.Users.Groups;
using GTAServer.PluginAPI.Entities;
using GTAServer.Logging;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace GTAServer
{
    public class GameServer
    {
        public string Location => Directory.GetCurrentDirectory();
        public NetPeerConfiguration Config;

        public List<Client> Clients { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public string GamemodeName { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected => !string.IsNullOrEmpty(Password);
        public List<string> MasterServers { get; } = new List<string>();
        public bool AnnounceSelf { get; set; }
        public bool AllowNicknames { get; set; }
        public bool AllowOutdatedClients { get; set; }
        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9_4;
        public string LastKickedIP { get; set; }
        public Client LastKickedClient { get; set; }
        public bool DebugMode { get; set; }
        public NetServer Server;
        public int CurrentTick { get; set; } = 0;

        public string Motd { get; set; } = "Welcome to this GTA CooP server!";
        public IPermissionProvider PermissionProvider { get; set; }
        public PrometheusMetrics Metrics { get; set; }
        public bool UPnP { get; set; }
        public string RconPassword { get; set; }

        public readonly Dictionary<Command, Action<CommandContext, List<string>>> Commands = new Dictionary<Command, Action<CommandContext, List<string>>>();

        public int TicksPerSecond { get; set; }

        private DateTime _lastAnnounceDateTime;
        public ILogger logger;
        private readonly Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();
        private int _ticksLastSecond;

        private readonly Timer _tpsTimer;
        public GameServer(int port, string name, string gamemodeName, bool isDebug, ServerConfiguration config)
        {
            logger = Util.LoggerFactory.CreateLogger<GameServer>();
            logger.LogInformation(LogEvent.Setup, "Server ready to start");
            Clients = new List<Client>();
            MaxPlayers = 32;
            GamemodeName = gamemodeName;
            Name = name;
            Port = port;
            UPnP = config.UPnP;

            Config = new NetPeerConfiguration("GTAVOnlineRaces") { Port = port, EnableUPnP = UPnP };
            if(config.DualStack)
            {
                Config.DualStack = true;
                Config.LocalAddress = IPAddress.IPv6Any;
            }

            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            Config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(Config);

            logger.LogInformation(LogEvent.Setup, $"NetServer created with port {Config.Port}");

            _tpsTimer = new Timer(state => CalculateTicksPerSecond(), null, 0, 1000);
        }

        public void Start()
        {
            logger.LogInformation(LogEvent.Start, "Server starting");

            logger.LogDebug(LogEvent.Plugin, "Loading gamemode");
            if (GamemodeName != "none" && !string.IsNullOrEmpty(GamemodeName))
            {
                var assemblyName = 
                    Location + Path.DirectorySeparatorChar + "Gamemodes" + Path.DirectorySeparatorChar + GamemodeName + ".dll";
                Assembly pluginAssembly = null;

                try
                {
#if !BUILD_WASM
                    pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyName);
#endif
                }
                catch (Exception)
                {
                    logger.LogWarning(LogEvent.Plugin, "Given gamemode couldn't be loaded, using none");
                }

                if (pluginAssembly != null)
                {

                    var types = pluginAssembly.GetExportedTypes();
                    var validTypes = types.Where(t => typeof(IGamemode).IsAssignableFrom(t)).ToArray();
                    if (!validTypes.Any())
                    {
                        logger.LogError(LogEvent.Plugin, "No gamemodes found in gamemode assembly, using none");
                        GamemodeName = "none";
                        return;
                    }
                    if (validTypes.Count() > 1)
                    {
                        logger.LogError(LogEvent.Plugin, "Multiple valid gamemodes found in gamemode assembly, using none");
                        GamemodeName = "none";
                        return;
                    }
                    var gamemode = Activator.CreateInstance(validTypes.First()) as IGamemode;
                    if (gamemode == null)
                    {
                        logger.LogError(LogEvent.Plugin,
                            "Could not create instance of gamemode (Activator.CreateInstance returned null), using none");
                        GamemodeName = "none";
                        return;
                    }
                    GamemodeName = gamemode.GamemodeName;
                    gamemode.OnEnable(this, false);
                }
            }
            logger.LogDebug(LogEvent.Plugin, "Gamemode loaded");

            try
            {
                Server.Start();
            }
            catch (SocketException e)
            {
                logger.LogCritical(LogEvent.Start, $"Couldn't bind port {Port}: {e.Message}");
                Environment.Exit(0);
            }
            catch (ArgumentOutOfRangeException)
            {
                logger.LogCritical(LogEvent.Start, $"Couldn't bind port {Port}: Not a valid 16-bit port number");
                Environment.Exit(0);
            }

            if (UPnP)
            {
                logger.LogInformation(LogEvent.Start, "Attempting to forward port " + Port);

                if(Server.UPnP.ForwardPort(Port, "GTAServer.core server"))
                {
                    var ip = Server.UPnP.GetExternalIP();
                    logger.LogInformation(LogEvent.UPnP, $"Server available on {ip}, Port = {Port}");
                }
            }

            if (AnnounceSelf)
            {
                AnnounceToMaster();
            }
        }

        internal sealed class MasterRequest
        {
            [JsonPropertyName("port")]
            public int Port { get; set; }

            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("max_players")]
            public int MaxPlayers { get; set; }

            [JsonPropertyName("gamemode")]
            public string GamemodeName { get; set; }

            [JsonPropertyName("telemetry")]
            public string Telemetry { get; set; }
        }

        private async void AnnounceToMaster()
        {
            if (DebugMode)
                return;

            logger.LogInformation(LogEvent.Announce, "Announcing to master server");
            _lastAnnounceDateTime = DateTime.Now;

            var client = Util.HttpClient;

            var content = new StringContent(Port.ToString(CultureInfo.InvariantCulture));

            for (var master = 0; master < MasterServers.Count; master++)
            {
                try
                {
                    // old master announce
                    await client.PostAsync(MasterServers[master], content);

                    // updated announce
                    var request = new MasterRequest
                    {
                        Port = Port,
                        MaxPlayers = MaxPlayers,
                        GamemodeName = GamemodeName,
                        Version = 0
                    };

                    try { Util.AppendTelemetry(ref request); } catch (Exception) { }

                    await client.PutAsync(MasterServers[master],
                        new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

                }
                catch (InvalidOperationException)
                {
                    logger.LogError(LogEvent.Announce, $"Failed to announce to master {master + 1}: URL is invalid");
                }
                catch (Exception e)
                {
                    logger.LogWarning(LogEvent.Announce, $"Failed to announce to master {master + 1}: {e.Message}");
                }
            }

            content.Dispose();
        }

        private void CalculateTicksPerSecond()
        {
            TicksPerSecond = CurrentTick - _ticksLastSecond;
            _ticksLastSecond = CurrentTick;

#if !BUILD_WASM
            Console.Title = "GTAServer - " + Name + " (" + Clients.Count + "/" + MaxPlayers + " players) - Port: " + Port + " - TPS: " + TicksPerSecond;
#endif
        }

        public void Tick()
        {
            CurrentTick++;
            GameEvents.Tick(CurrentTick);

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                AnnounceToMaster();
            }

            NetIncomingMessage msg;
            while ((msg = Server.ReadMessage()) != null)
            {
                Client client = null;
                lock (Clients)
                {
                    foreach (var c in Clients)
                    {
                        if (c?.NetConnection == null || c.NetConnection.RemoteUniqueIdentifier == 0 ||
                            msg.SenderConnection == null ||
                            c.NetConnection.RemoteUniqueIdentifier != msg.SenderConnection.RemoteUniqueIdentifier)
                            continue;
                        client = c;
                        break;
                    }
                }
                if (client == null)
                {
                    logger.LogDebug(LogEvent.Connection, "Client not found for remote ID " + msg.SenderConnection?.RemoteUniqueIdentifier + ", creating client. Current number of clients: " + Clients.Count());
                    client = new Client(msg.SenderConnection, this);
                }

                // Plugin event: OnIncomingPacket
                var pluginPacketHandlerResult = PacketEvents.IncomingPacket(client, msg);
                msg = pluginPacketHandlerResult.Data;
                if (!pluginPacketHandlerResult.ContinueServerProc)
                {
                    Server.Recycle(msg);
                    return;
                }

                //logger.LogInformation("Packet received - type: " + ((NetIncomingMessageType)msg.MessageType).ToString());
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.UnconnectedData:
                        if (msg.IsOob)
                        {
                            var str = msg.ReadNullTerminatedString();
                            if (str.StartsWith("rcon")) HandleRconConnection(msg, str);
                            if (str.StartsWith("metrics")) Metrics.HandleConnection(this, msg, str);

                            return;
                        }

                        var ucType = msg.ReadString();
                        // ReSharper disable once ConvertIfStatementToSwitchStatement
                        if (ucType == "ping")
                        {
                            if (!PacketEvents.Ping(client, msg).ContinueServerProc)
                            {
                                Server.Recycle(msg);
                                return;
                            }

                            logger.LogInformation(LogEvent.Connection, "Ping received from " + msg.SenderEndPoint.Address.ToString());

                            var reply = Server.CreateMessage("pong");
                            Server.SendUnconnectedMessage(reply, msg.SenderEndPoint);
                        }
                        else if (ucType == "query")
                        {
                            if (!PacketEvents.Query(client, msg).ContinueServerProc)
                            {
                                Server.Recycle(msg);
                                return;
                            }

                            logger.LogInformation(LogEvent.Connection, "Query received from " + msg.SenderEndPoint.Address.ToString());

                            // does anyone even use this?
                            object[] response = { Name, GamemodeName, Port, Clients.Count, MaxPlayers,
                                string.Join(",", Clients.Select(x => x.DisplayName)) };

                            var reply = Server.CreateMessage(                             // escape ; if someone tries to parse it
                                string.Join(";", response.ToList().Select(x => x.ToString().Replace(";", "\\;"))) + ";");

                            Server.SendUnconnectedMessage(reply, msg.SenderEndPoint);
                        }
                        else if(ucType == "players")
                        {
                            var playerList = new PlayerList { };
                            lock (Clients)
                                foreach(var c in Clients)
                                {
                                    var address = c.NetConnection
                                        .RemoteEndPoint
                                        .Address
                                        .GetAddressBytes()
                                        .Take(3);

                                    playerList.Members.Add(new PlayerListMember
                                    {
                                        Name = c.DisplayName,
                                        Address = address.ToArray(),
                                        GameVersion = c.GameVersion,
                                        Latency = (int)TimeSpan.FromSeconds(c.Latency).TotalMilliseconds,
                                    });
                                }

                            var data = Util.SerializeBinary(playerList);

                            var response = Server.CreateMessage();
                            response.Write(1001); // unconnected messages start at 1000/1001ish to not break existing protocols
                            response.Write(data.Length);
                            response.Write(data);
                            Server.SendUnconnectedMessage(response, msg.SenderEndPoint);
                        }
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                        logger.LogDebug("Network (Verbose)DebugMessage: " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        logger.LogWarning("Network WarningMessage: " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        logger.LogError("Network ErrorMessage: " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        client.Latency = msg.ReadFloat();
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        var connectionApprovalPacketResult = PacketEvents.IncomingConnectionApproval(client, msg);
                        msg = connectionApprovalPacketResult.Data;
                        if (!connectionApprovalPacketResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientConnectionApproval(client, msg);
                        break;
                    case NetIncomingMessageType.Error:
                        logger.LogError("Network Error: " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        pluginPacketHandlerResult = PacketEvents.IncomingStatusChange(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientStatusChange(client, msg);
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        pluginPacketHandlerResult = PacketEvents.IncomingDiscoveryRequest(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientDiscoveryRequest(client, msg);
                        break;
                    case NetIncomingMessageType.Data:
                        pluginPacketHandlerResult = PacketEvents.IncomingData(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientIncomingData(client, msg);
                        break;
                    default:
                        // We shouldn't get packets reaching this, so throw warnings when it happens.
                        logger.LogWarning("Unknown packet received: " +
                                          msg.MessageType.ToString());
                        break;

                }
                Server.Recycle(msg);
            }

            Metrics.Tick(this);
        }

        private void HandleRconConnection(NetIncomingMessage msg, string str)
        {
            if (string.IsNullOrEmpty(RconPassword))
            {
                RespondRconMessage(msg.SenderEndPoint,
                    "This feature has been disabled, enable it by setting RconPassword in the server configuration");
                return;
            }

            var split = str.Split(' ');
            if (split[1] != RconPassword)
            {
                RespondRconMessage(msg.SenderEndPoint, "Invalid rcon password");
                return;
            }

            var command = string.Join(" ", split.Skip(2));
            logger.LogInformation(LogEvent.Rcon, $"{msg.SenderEndPoint.Address.MapToIPv4()}: {command}");

            // construct a new rcon commandsender
            var sender = new RconCommandSender();
            sender.Destination = msg.SenderEndPoint;
            sender.GameServer = this;

            // invoke command
            var found = ServerManager.ExecuteCommand(command, this, sender);
            if(!found)
            {
                RespondRconMessage(msg.SenderEndPoint, "Command not found");
            }
        }

        internal void RespondRconMessage(IPEndPoint destination, string response)
        {
            response = "print " + response + "\n";
            var message = new byte[] {0xff, 0xff, 0xff, 0xff}.Concat(
                Encoding.UTF8.GetBytes(response))
                .ToArray();

            Server.RawSend(message, 0, message.Length, destination);
        }

        private void HandleClientConnectionApproval(Client client, NetIncomingMessage msg)
        {
            var type = msg.ReadInt32();
            var length = msg.ReadInt32();
            var connReq = Util.DeserializeBinary<ConnectionRequest>(msg.ReadBytes(length));
            if (connReq == null)
            {
                DenyConnect(client, "Connection is null, this is most likely a bug in the client.", true, msg);
                return;
            }

            var pluginResponse = ConnectionEvents.ConnectionRequest(client, connReq);
            if (!pluginResponse.ContinueServerProc) return;
            connReq = pluginResponse.Data;

            client.DisplayName = connReq.DisplayName;
            client.Name = connReq.Name;
            client.GameVersion = connReq.GameVersion;
            client.RemoteScriptVersion = (ScriptVersion)connReq.ScriptVersion;

            // If nicknames are disabled on the server, set the nickname to the player's social club name.
            if (!AllowNicknames)
            {
                SendNotificationToPlayer(client,
                    $"Nicknames are disabled on this server. Your nickname has been set to {connReq.Name}");
                client.DisplayName = client.Name;
            }


            logger.LogInformation(LogEvent.Handshake,
                $"New connection request: {client.DisplayName}@{msg.SenderEndPoint.Address} | Game version: {client.GameVersion} | Script version: {client.RemoteScriptVersion}");

            var latestScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last();
            var latestReadableScriptVersion = latestScriptVersion.ToReadable();

            // check version
            if (client.RemoteScriptVersion == ScriptVersion.VERSION_UNKNOWN)
            {
                logger.LogInformation(LogEvent.Handshake, $"Client {client.DisplayName} tried to connect with an unknown script version (client too old?)");

                DenyConnect(client, $"Unknown version. Please re-download GTA Coop from www.gtacoop.com", true, msg);
                return;
            }
            if (!AllowOutdatedClients && connReq.ScriptVersion < (byte)latestScriptVersion)
            {
                // client outdated
                logger.LogInformation(LogEvent.Handshake, $"Client {client.DisplayName} tried to connect with an outdated script version {client.RemoteScriptVersion} but the server requires {latestScriptVersion}");

                DenyConnect(client, $"Please update to version {latestReadableScriptVersion} from www.gtacoop.com", true, msg);
                return;
            }
            else if (!AllowOutdatedClients && connReq.ScriptVersion > (byte)latestScriptVersion)
            {
                // server outdated?
                logger.LogInformation(LogEvent.Handshake, $"Client {client.DisplayName} tried to connect with a newer client version {connReq.ScriptVersion}, please make sure the server is up-to-date (current: {latestScriptVersion}, {(byte)latestScriptVersion})");

                DenyConnect(client, $"This server requires an older version ({latestReadableScriptVersion})", true, msg);
                return;
            }
            else if(client.RemoteScriptVersion != latestScriptVersion)
            {
                SendNotificationToPlayer(client, "You are currently on an outdated client. Please go to www.gtacoop.com and update.");
            }

            var numClients = 0;
            lock (Clients) numClients = Clients.Count;
            if (numClients >= MaxPlayers)
            {
                logger.LogInformation(LogEvent.Handshake, $"Player tried to join while server is full: {client.DisplayName}");
                DenyConnect(client, "No available player slots.", true, msg);
                return;
            }

            if (PasswordProtected && connReq.Password != Password)
            {
                logger.LogInformation(LogEvent.Handshake, $"Client {client.DisplayName} tried to connect with the wrong password.");
                DenyConnect(client, "Wrong password.", true, msg);
                return;
            }

            logger.LogTrace(LogEvent.Handshake, "ConnectionRequest");
            logger.LogTrace(LogEvent.Handshake, "DisplayName " + connReq.DisplayName);
            logger.LogTrace(LogEvent.Handshake, "Name " + connReq.Name);

            lock (Clients)
                if (Clients.Any(c => c.DisplayName == client.DisplayName))
                {
                    DenyConnect(client, "A player already exists with the current display name.");
                    return;
                }
                else
                {
                    Clients.Add(client);
                }



            var channelHail = Server.CreateMessage();
            channelHail.Write(GetChannelForClient(client));
            client.NetConnection.Approve(channelHail);
        }
        private void HandleClientStatusChange(Client client, NetIncomingMessage msg)
        {
            var newStatus = (NetConnectionStatus)msg.ReadByte();
            switch (newStatus)
            {
                case NetConnectionStatus.Connected:
                    logger.LogInformation(LogEvent.StatusChange, $"Connected: {client.DisplayName}@{msg.SenderEndPoint.Address}");
                    SendNotificationToAll($"Player connected: {client.DisplayName}");

                    if (!string.IsNullOrEmpty(Motd)) 
                    { 
                        SendChatMessageToPlayer(client, Motd);
                    }

                    ConnectionEvents.Join(client);

                    break;

                case NetConnectionStatus.Disconnected:
                    if (Clients.Contains(client))
                    {
                        if (!client.Silent)
                        {
                            if (client.Kicked)
                            {
                                if (string.IsNullOrEmpty(client.KickReason)) client.KickReason = "Unknown";
                                SendNotificationToAll(
                                    $"Kicked: {client.DisplayName} - Reason: {client.KickReason}");
                            }
                            else
                            {
                                SendNotificationToAll(
                                    $"Player disconnected: {client.DisplayName}");
                            }
                        }
                        var dcMsg = new PlayerDisconnect()
                        {
                            Id = client.NetConnection.RemoteUniqueIdentifier
                        };

                        SendToAll(dcMsg, PacketType.PlayerDisconnect, true);

                        if (client.Kicked)
                        {
                            logger.LogInformation(LogEvent.StatusChange,
                                $"Player kicked: {client.DisplayName}@{msg.SenderEndPoint.Address}");
                            LastKickedClient = client;
                            LastKickedIP = client.NetConnection.RemoteEndPoint.ToString();
                        }
                        else
                        {
                            logger.LogInformation(LogEvent.StatusChange, $"Player disconnected: {client.DisplayName}@{msg.SenderEndPoint.Address}");
                        }

                        lock (Clients)
                            Clients.Remove(client);
                        ConnectionEvents.Disconnect(client);
                    }
                    break;
                // resharper was bugging me about not having the below case statements
                case NetConnectionStatus.None:
                case NetConnectionStatus.InitiatedConnect:
                case NetConnectionStatus.ReceivedInitiation:
                case NetConnectionStatus.RespondedAwaitingApproval:
                case NetConnectionStatus.RespondedConnect:
                case NetConnectionStatus.Disconnecting:
                default:
                    break;
            }
        }
        private void HandleClientDiscoveryRequest(Client client, NetIncomingMessage msg)
        {
            var latestScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last();

            var responsePkt = Server.CreateMessage();
            var discoveryResponse = new DiscoveryResponse
            {
                ServerName = Name,
                MaxPlayers = MaxPlayers,
                PasswordProtected = PasswordProtected,
                Gamemode = GamemodeName,
                Port = Port,
                Version = (byte)latestScriptVersion
            };
            lock (Clients) discoveryResponse.PlayerCount = Clients.Count;

            var serializedResponse = Util.SerializeBinary(discoveryResponse);
            responsePkt.Write((int)PacketType.DiscoveryResponse);
            responsePkt.Write(serializedResponse.Length);
            responsePkt.Write(serializedResponse);
            logger.LogInformation(LogEvent.Connection, $"Server status requested by {msg.SenderEndPoint.Address}");
            Server.SendDiscoveryResponse(responsePkt, msg.SenderEndPoint);
        }

        private void HandleClientIncomingData(Client client, NetIncomingMessage msg)
        {
            if (msg.LengthBytes < 4)
            {
                logger.LogWarning(LogEvent.Incoming, "Received invalid packet from " + client.DisplayName);
                return;
            }

            var packetType = (PacketType)msg.ReadInt32();

            switch (packetType)
            {
                case PacketType.ChatData:
                    {
                        // TODO: This code really could use refactoring.. right now only trying to make sure this all works on .NET Core and fixing small issues.
                        var len = msg.ReadInt32();
                        var chatData = Util.DeserializeBinary<ChatData>(msg.ReadBytes(len));
                        if (chatData != null)
                        {
                            // Plugin chat handling
                            var chatPluginResult = GameEvents.ChatMessage(client, chatData);
                            if (!chatPluginResult.ContinueServerProc) return;
                            chatData = chatPluginResult.Data;

                            // Command handling
                            if (chatData.Message.StartsWith("/"))
                            {
                                var cmdArgs = Util.SplitCommandString(chatData.Message);
                                var cmdName = cmdArgs[0].Remove(0, 1);
                                if (Commands.Any(x => x.Key.Name == cmdName))
                                {
                                    if (HasPermission(client, PermissionType.Command, cmdName))
                                    {
                                        var ctx = new CommandContext
                                        {
                                            Client = client,
                                            GameServer = this,
                                            ChatData = chatData,
                                            Sender = client
                                        };

                                        var command = Commands.First(x => x.Key.Name == cmdName);
                                        command.Value.Invoke(ctx, cmdArgs.Skip(1).ToList());
                                    }
                                    else
                                    {
                                        SendChatMessageToPlayer(client, "You don't have the permission to execute this command");
                                    }

                                    return;
                                }
                                SendChatMessageToPlayer(client, "Command not found");

                                return;
                            }
                            var chatMsg = new ChatMessage(chatData, client);
                            if (!chatMsg.Suppress)
                            {
                                chatData.Id = client.NetConnection.RemoteUniqueIdentifier;
                                chatData.Sender = "";
                                if (!string.IsNullOrWhiteSpace(chatMsg.Prefix))
                                    chatData.Sender += "[" + chatMsg.Prefix + "] ";
                                chatData.Sender += chatMsg.Sender.DisplayName;

                                if (!string.IsNullOrWhiteSpace(chatMsg.Suffix))
                                    chatData.Sender += $" ({chatMsg.Suffix}) ";

                                chatData.Message = Util.SanitizeString(chatData.Message);

                                SendToAll(chatData, PacketType.ChatData, true);
                                logger.LogInformation(LogEvent.Chat, $"<{chatData.Sender}>: {chatData.Message}");
                            }
                        }
                    }
                    break;
                case PacketType.VoiceChatData:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        if (data != null)
                        {
                            // voice packet looks diff so send it manually
                            var msgSend = Server.CreateMessage();

                            msgSend.Write((int)PacketType.VoiceChatData);

                            msgSend.Write(1);
                            msgSend.Write(len);
                            msgSend.Write(data);

                            Server.SendToAll(msgSend, client.NetConnection, NetDeliveryMethod.Unreliable, 0);
                        }
                    }
                    break;
                case PacketType.VehiclePositionData:
                    {
                        var len = msg.ReadInt32();
                        var vehicleData = Util.DeserializeBinary<VehicleData>(msg.ReadBytes(len));
                        if (vehicleData != null)
                        {
                            var vehiclePluginResult = GameEvents.VehicleDataUpdate(client, vehicleData);
                            if (!vehiclePluginResult.ContinueServerProc) return;
                            vehicleData = vehiclePluginResult.Data;

                            vehicleData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            vehicleData.Name = client.DisplayName;
                            vehicleData.Latency = client.Latency;

                            client.Health = vehicleData.PlayerHealth;
                            client.LastKnownPosition = vehicleData.Position;
                            client.IsInVehicle = true;

                            SendToAll(vehicleData, PacketType.VehiclePositionData, false, client);
                        }
                    }
                    break;
                case PacketType.PedPositionData:
                    {
                        var len = msg.ReadInt32();
                        var pedPosData = Util.DeserializeBinary<PedData>(msg.ReadBytes(len));
                        if (pedPosData != null)
                        {
                            var pedPluginResult = GameEvents.PedDataUpdate(client, pedPosData);
                            if (!pedPluginResult.ContinueServerProc) return;
                            pedPosData = pedPluginResult.Data;

                            pedPosData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            pedPosData.Name = client.DisplayName;
                            pedPosData.Latency = client.Latency;

                            client.Health = pedPosData.PlayerHealth;
                            client.LastKnownPosition = pedPosData.Position;
                            client.IsInVehicle = false;

                            SendToAll(pedPosData, PacketType.PedPositionData, false, client);
                        }
                    }
                    break;
                case PacketType.NpcVehPositionData:
                    {
                        var len = msg.ReadInt32();
                        var vehData = Util.DeserializeBinary<VehicleData>(msg.ReadBytes(len));

                        if (vehData != null)
                        {
                            var pluginVehData = GameEvents.NpcVehicleDataUpdate(client, vehData);
                            if (!pluginVehData.ContinueServerProc) return;
                            vehData = pluginVehData.Data;

                            vehData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            SendToAll(vehData, PacketType.NpcVehPositionData, false, client);
                        }
                    }
                    break;
                case PacketType.NpcPedPositionData:
                    {
                        var len = msg.ReadInt32();
                        var pedData = Util.DeserializeBinary<PedData>(msg.ReadBytes(len));
                        if (pedData != null)
                        {
                            var pluginPedData = GameEvents.NpcPedDataUpdate(client, pedData);
                            if (!pluginPedData.ContinueServerProc) return;
                            pedData = pluginPedData.Data;

                            pedData.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                        }
                        SendToAll(pedData, PacketType.NpcPedPositionData, false, client);
                    }
                    break;
                case PacketType.WorldSharingStop:
                    {
                        GameEvents.WorldSharingStop(client);
                        var dcObj = new PlayerDisconnect()
                        {
                            Id = client.NetConnection.RemoteUniqueIdentifier
                        };
                        SendToAll(dcObj, PacketType.WorldSharingStop, true);
                    }
                    break;
                case PacketType.NativeResponse:
                    {
                        var len = msg.ReadInt32();
                        var nativeResponse = Util.DeserializeBinary<NativeResponse>(msg.ReadBytes(len));
                        if (nativeResponse == null || !_callbacks.ContainsKey(nativeResponse.Id)) return;
                        object response = nativeResponse.Response;
                        if (response is IntArgument)
                        {
                            response = ((IntArgument)response).Data;
                        }
                        else if (response is UIntArgument)
                        {
                            response = ((UIntArgument)response).Data;
                        }
                        else if (response is StringArgument)
                        {
                            response = ((StringArgument)response).Data;
                        }
                        else if (response is FloatArgument)
                        {
                            response = ((FloatArgument)response).Data;
                        }
                        else if (response is BooleanArgument)
                        {
                            response = ((BooleanArgument)response).Data;
                        }
                        else if (response is Vector3Argument)
                        {
                            var tmp = (Vector3Argument)response;
                            response = new Vector3()
                            {
                                X = tmp.X,
                                Y = tmp.Y,
                                Z = tmp.Z
                            };
                        }
                        _callbacks[nativeResponse.Id].Invoke(response);
                        _callbacks.Remove(nativeResponse.Id);
                    }
                    break;
                case PacketType.PlayerSpawned:
                    {
                        GameEvents.PlayerSpawned(client);
                        logger.LogInformation("Player spawned: " + client.DisplayName);
                    }
                    break;
                case PacketType.PluginMessage:
                    {
                        var name = msg.ReadString();

                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        var message = new PluginMessage
                        {
                            Name = name,
                            Data = data
                        };

                        PacketEvents.PluginMessage(client, message);
                    }
                    break;
                // The following is normally only received on the client.
                case PacketType.PlayerDisconnect:
                    break;
                case PacketType.DiscoveryResponse:
                    break;
                case PacketType.ConnectionRequest:
                    break;
                case PacketType.NativeCall:
                    break;
                case PacketType.NativeTick:
                    break;
                case PacketType.NativeTickRecall:
                    break;
                case PacketType.NativeOnDisconnect:
                    break;
                case PacketType.NativeOnDisconnectRecall:
                    break;
                default:
                    // ReSharper disable once NotResolvedInText
                    // resharper wants to see a variable name in the below... w/e.
                    logger.LogWarning(LogEvent.Incoming, "Received unknown packet type " + (int)packetType + " from " + client.DisplayName);
                    break;
            }
        }

        /// <summary>
        /// Init a configuration for the current plugin, this will create a (pluginname).xml in the configuration option
        /// </summary>
        /// <param name="plugin">The current plugin class, the file will be named to the classname</param>
        /// <returns>The provided configuration object</returns>
        public T InitConfiguration<T>(Type plugin)
        {
            var name = plugin.Name.ToLower();
            var path = Location + Path.DirectorySeparatorChar + "Configuration" + Path.DirectorySeparatorChar + name + ".xml";

            var serializer = new XmlSerializer(typeof(T));
            T cfg;

            if (File.Exists(path))
            {
                cfg = Activator.CreateInstance<T>();
                using (var stream = File.OpenRead(path)) cfg = (T)serializer.Deserialize(stream);
                using (
                    var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create,
                        FileAccess.ReadWrite)) serializer.Serialize(stream, cfg);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) serializer.Serialize(stream, cfg = Activator.CreateInstance<T>());
            }

            return cfg;
        }

        /// <summary>
        /// Registers a command to the server
        /// </summary>
        /// <param name="name">The name of the command</param>
        /// <param name="callback">The callback which will get triggered while executing the command</param>
        public void RegisterCommand(string name, Action<CommandContext, List<string>> callback)
        {
            var command = new Command { Name = name };

            RegisterCommand(command, callback);
        }

        /// <summary>
        /// Registers a command to the server
        /// </summary>
        /// <param name="command">The command entry</param>
        /// <param name="callback">The callback which will get triggered while executing the command</param>
        public void RegisterCommand(Command command, Action<CommandContext, List<string>> callback)
        {
            if (Commands.ContainsKey(command))
                throw new Exception("A command with this name has already been registered");

            Commands.Add(command, callback);
        }

        /// <summary>
        /// Registers all commands from a class
        /// </summary>
        /// <typeparam name="T">The class to load commands from</typeparam>
        public void RegisterCommands<T>()
        {
            var commands = typeof(T).GetMethods().Where(method => method.GetCustomAttributes(typeof(CommandAttribute), false).Any());

            foreach (var method in commands)
            {
                var attribute = method.GetCustomAttribute<CommandAttribute>(true);
                var command = new Command
                {
                    Name = attribute.Name,
                    Description = attribute.Description,
                    Usage = attribute.Usage
                };

                RegisterCommand(command, (Action<CommandContext, List<string>>)Delegate.CreateDelegate(typeof(Action<CommandContext, List<string>>), method));
            }
        }

        /// <summary>
        /// Returns if the <see cref="Client"/> has the provided permissions
        /// </summary>
        /// <param name="client">The client to check on</param>
        /// <param name="type">The permission type</param>
        /// <param name="permission">The permission name to check</param>
        /// <returns></returns>
        public bool HasPermission(Client client, PermissionType type, string permission)
        {
            if (PermissionProvider == null)
                return false;

            return PermissionProvider.HasPermission(client, type, permission);
        }

        /// <summary>
        /// Sends a packet to all players
        /// </summary>
        /// <param name="dataToSend">The object to be serialized and send</param>
        /// <param name="packetType">The packet type</param>
        /// <param name="packetIsImportant">Whether should be send ordered</param>
        public void SendToAll(object dataToSend, PacketType packetType, bool packetIsImportant)
        {
            var data = Util.SerializeBinary(dataToSend);
            var msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, packetIsImportant ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        /// Sends a packet to all players
        /// </summary>
        /// <param name="dataToSend">The object to be serialized and send</param>
        /// <param name="packetType">The packet type</param>
        /// <param name="packetIsImportant">Whether should be send ordered</param>
        /// <param name="clientToExclude">The client this packet should not be send to</param>
        public void SendToAll(object dataToSend, PacketType packetType, bool packetIsImportant, Client clientToExclude)
        {
            var data = Util.SerializeBinary(dataToSend);
            var msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, clientToExclude.NetConnection, packetIsImportant ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, GetChannelForClient(clientToExclude));
        }

        /// <summary>
        /// Deny a player during handshake, this is supposed to be called during a connection request
        /// </summary>
        /// <param name="player">The player to deny</param>
        /// <param name="reason">The reason to be displayed</param>
        /// <param name="silent">Whether or not the deny should not be display to other clients</param>
        /// <param name="msg">The orginal incoming message</param>
        public void DenyConnect(Client player, string reason, bool silent = true, NetIncomingMessage msg = null)
        {
            player.NetConnection.Deny(reason);
            logger.LogInformation(LogEvent.Handshake, $"Player rejected from server: {player.DisplayName} for {reason}");
            if (!silent)
            {
                SendNotificationToAll($"Player rejected by server: {player.DisplayName} - {reason}");
            }

            Clients.Remove(player);
            //if (msg != null) Server.Recycle(msg);
        }

        public int GetChannelForClient(Client c)
        {
            lock (Clients) return (Clients.IndexOf(c) % 31) + 1;
        }

        // Native call functions

        private List<NativeArgument> ParseNativeArguments(params object[] args) // literally copypasted from old gtaserver
        {
            var list = new List<NativeArgument>();
            foreach (var o in args)
            {
                if (o is int)
                {
                    list.Add(new IntArgument() { Data = ((int)o) });
                }
                else if (o is uint)
                {
                    list.Add(new UIntArgument() { Data = ((uint)o) });
                }
                else if (o is string)
                {
                    list.Add(new StringArgument() { Data = ((string)o) });
                }
                else if (o is float)
                {
                    list.Add(new FloatArgument() { Data = ((float)o) });
                }
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is Vector3)
                {
                    var tmp = (Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
                }
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }

            return list;
        }

        /// <summary>
        /// Sends a native call to the player
        /// </summary>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SendNativeCallToPlayer(Client player, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Sends a native call to all players
        /// </summary>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SendNativeCallToAll(ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments),
                ReturnType = null,
                Id = null,
            };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a native call to the player and receive the return value in a callback
        /// </summary>
        /// <param name="salt">Identifier to make this native call unique</param>
        /// <param name="hash">The hash of the native</param>
        /// <param name="returnType">The return type of the native</param>
        /// <param name="callback">The callback to call after the native returned</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void GetNativeCallFromPlayer(Client player, string salt, ulong hash, NativeArgument returnType,
            Action<object> callback, params object[] arguments)
        {
            var obj = new NativeData()
            {
                Hash = hash,
                ReturnType = returnType
            };
            salt = Environment.TickCount64.ToString() + salt + player.NetConnection.RemoteUniqueIdentifier.ToString();
            obj.Id = salt;
            obj.Arguments = ParseNativeArguments(arguments);
            var bin = Util.SerializeBinary(obj);
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);
            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Instructs a player to call a native on each tick
        /// </summary>
        /// <param name="identifier">An unique identifier for this tick native, can be used to recall it later</param>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SetNativeCallOnTickForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };


            var wrapper = new NativeTickCall();
            wrapper.Id = identifier;
            wrapper.Native = obj;

            var bin = Util.SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Recall a tick native on a player
        /// </summary>
        /// <param name="identifier">The unique identifier of the tick native</param>
        public void RecallNativeCallOnTickForPlayer(Client player, string identifier)
        {
            var wrapper = new NativeTickCall { Id = identifier };

            var bin = Util.SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Instructs all players to call a native on each tick
        /// </summary>
        /// <param name="identifier">An unique identifier for this tick native, can be used to recall it later</param>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SetNativeCallOnTickForAll(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };


            var wrapper = new NativeTickCall
            {
                Id = identifier,
                Native = obj
            };

            var bin = Util.SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Recall a tick native on all players
        /// </summary>
        /// <param name="identifier">The unique identifier of the tick native</param>
        public void RecallNativeCallOnTickForAll(string identifier)
        {
            var wrapper = new NativeTickCall { Id = identifier };

            var bin = Util.SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Instructs a player to call a native on disconnect
        /// </summary>
        /// <param name="identifier">An unique identifier for this disconnect native, can be used to recall it later</param>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SetNativeCallOnDisconnectForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Id = identifier,
                Arguments = ParseNativeArguments(arguments)
            };


            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Instructs all players to call a native on disconnect
        /// </summary>
        /// <param name="identifier">An unique identifier for this disconnect native, can be used to recall it later</param>
        /// <param name="hash">The hash of the native</param>
        /// <param name="arguments">Any additional parameters to call the native with</param>
        public void SetNativeCallOnDisconnectForAll(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Id = identifier,
                Arguments = ParseNativeArguments(arguments)
            };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Recall a disconnect native on a player
        /// </summary>
        /// <param name="identifier">The unique identifier of the disconnect native</param>
        public void RecallNativeCallOnDisconnectForPlayer(Client player, string identifier)
        {
            var obj = new NativeData { Id = identifier };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        /// <summary>
        /// Recall a disconnect native on all players
        /// </summary>
        /// <param name="identifier">The unique identifier of the disconnect native</param>
        public void RecallNativeCallOnDisconnectForAll(string identifier)
        {
            var obj = new NativeData { Id = identifier };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }


        // Stuff for scripting

        // Notification stuff
        public void SendNotificationToPlayer(Client player, string message, bool flashing = false)
        {
            for (var i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
                SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendNotificationToAll(string message, bool flashing = false)
        {
            for (var i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToAll(0x202709F4C58A0424, "STRING");
                SendNativeCallToAll(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToAll(0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendPictureNotificationToAll(string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAll(0x202709F4C58A0424, "STRING");
            SendNativeCallToAll(0x6C188BE134E074AA, body);
            SendNativeCallToAll(0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToAll(0xF020C96915705B3A, false, true);
        }

        public void SendPictureNotificationToAll(string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAll(0x202709F4C58A0424, "STRING");
            SendNativeCallToAll(0x6C188BE134E074AA, body);
            SendNativeCallToAll(0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            SendNativeCallToAll(0xF020C96915705B3A, false, true);
        }

        /// <summary>
        /// Sends a chat message to all players
        /// </summary>
        /// <param name="msg">The message to send</param>
        public void SendChatMessageToAll(string msg) => SendChatMessageToAll("", msg);

        /// <summary>
        /// Sends a chat message to all players
        /// </summary>
        /// <param name="sender">The sender in the chat message</param>
        /// <param name="message">The message to send</param>
        public void SendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message
            };
            SendToAll(chatObj, PacketType.ChatData, true);
        }

        /// <summary>
        /// Sends a chat message to a player
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendChatMessageToPlayer(Client player, string message) => SendChatMessageToPlayer(player, "", message);

        /// <summary>
        /// Sends a chat message to a player
        /// </summary>
        /// <param name="sender">The sender in the chat message</param>
        /// <param name="message">The message to send</param>
        public void SendChatMessageToPlayer(Client player, string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message
            };
            var data = Util.SerializeBinary(chatObj);
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.ChatData);
            msg.Write(data.Length);
            msg.Write(data);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        /// <summary>
        /// Kicks a player from the server
        /// </summary>
        /// <param name="reason">The message displayed in the kick</param>
        /// <param name="silent">Whether or not the kick should not be displayed to other players</param>
        /// <param name="sender">The client who kicked the player</param>
        public void KickPlayer(Client player, string reason = null, bool silent = false, Client sender = null)
        {
            player.Kicked = true;
            player.KickReason = reason?.ToString();
            player.Silent = silent;
            player.KickedBy = sender;
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        /// <summary>
        /// Sets the position of a player
        /// </summary>
        /// <param name="newPosition">The new position of the player</param>
        public void SetPlayerPosition(Client player, Vector3 newPosition) => 
            SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), 
                newPosition.X, newPosition.Y, newPosition.Z, 1, 0, 0, 1);

        /// <summary>
        /// Gets the position of a player
        /// </summary>
        /// <param name="callback">The callback to call after the position returned</param>
        /// <param name="salt">Identifier to make this native call unique</param>
        public void GetPlayerPosition(Client player, Action<object> callback, string salt = "salt") =>
            GetNativeCallFromPlayer(player, salt, 0x3FEF770D40960D5A, new Vector3Argument(), 
                callback, new LocalPlayerArgument(), 0);

        public void GivePlayerWeapon(Client player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded) =>
            SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow,
                ammo);

        public void HasPlayerControlBeenPressed(Client player, int controlId, Action<object> callback, string salt = "salt") => 
            GetNativeCallFromPlayer(player, salt, 0x580417101DDB492F, new BooleanArgument(), 
                callback, 0, controlId);

        public void SetPlayerHealth(Client player, int health) => 
            SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), 
                health + 100);

        public void GetPlayerHealthg(Client player, Action<object> callback, string salt = "salt") => 
            GetNativeCallFromPlayer(player, salt, 0xEEF059FAD016D209, new IntArgument(),
                callback, new LocalPlayerArgument());

        public void SetNightVisionForPlayer(Client player, bool status) =>
            SendNativeCallToPlayer(player, 0x18F621F7A5B1F85D, status);

        public void SetNightVisionForAll(Client player, bool status) =>
            SendNativeCallToAll(0x18F621F7A5B1F85D, status);

        public void IsNightVisionActive(Client player, Action<object> callback, string salt = "salt") =>
            GetNativeCallFromPlayer(player, salt, 0x2202A3F42C8E5F79, new BooleanArgument(), 
                callback, new LocalPlayerArgument());

        /// <summary>
        /// Sends a world cleanup request to a player
        /// </summary>
        public void SendWorldCleanUpToPlayer(Client player)
        {
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.WorldCleanUpRequest);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        /// <summary>
        /// Sends a world cleanup request to all players
        /// </summary>
        public void SendWorldCleanUpToAll()
        {
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.WorldCleanUpRequest);
            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a plugin message to a player
        /// </summary>
        /// <param name="name">The unique plugin message name</param>
        /// <param name="data">The data of your message</param>
        /// <param name="reliable">Whether or not the message should be send reliable</param>
        public void SendPluginMessageToPlayer(Client player, string name, byte[] data, bool reliable = true)
        {
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.PluginMessage);
            msg.Write(name);
            msg.Write(data.Length);
            msg.Write(data);

            var method = reliable ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.Unreliable;

            player.NetConnection.SendMessage(msg, method, 0);
        }

        /// <summary>
        /// Sends a plugin message to all players
        /// </summary>
        /// <param name="name">The unique plugin message name</param>
        /// <param name="data">The data of your message</param>
        /// <param name="reliable">Whether or not the message should be send reliable</param>
        public void SendPluginMessageToAll(string name, byte[] data, bool reliable = true)
        {
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.PluginMessage);
            msg.Write(name);
            msg.Write(data.Length);
            msg.Write(data);

            var method = reliable ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.Unreliable;

            Server.SendToAll(msg, method);
        }
    }
}
