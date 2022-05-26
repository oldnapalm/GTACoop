using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;
using Race.Commands;
using Race.Objects;

namespace Race
{
    public class Race : IGamemode
    {
        public const ulong REQUEST_MODEL = 0x963D27A58DF860AC;
        public const ulong SET_MODEL_AS_NO_LONGER_NEEDED = 0xE532F5D78798DAAB;
        public const ulong DRAW_MARKER = 0x28477EC23D892089;
        public const ulong ADD_BLIP_FOR_COORD = 0x5A039BB0BCA604B6;
        public const ulong SET_BLIP_ALPHA = 0x45FF974EEE1C8734;
        public const ulong CREATE_VEHICLE = 0xAF35D0D2583051B0;
        public const ulong SET_PED_INTO_VEHICLE = 0xF75B0D629E1C063D;
        public const ulong TASK_LEAVE_VEHICLE = 0xD3DBCE61A490BE02;
        public const ulong SET_VEHICLE_FIXED = 0x115722B1B9C14C1C;
        public const ulong SET_VEHICLE_ENGINE_ON = 0x2497C4717C8B881E;
        public const ulong CREATE_OBJECT = 0x509D5878EB39E842;
        public const ulong FREEZE_ENTITY_POSITION = 0x428CA6DBD1094446;
        public const ulong SET_ENTITY_COORDS = 0x06843DA7060A026B;
        public const ulong SET_ENTITY_ROTATION = 0x8524A8B0171D5E07;
        public const ulong SET_ENTITY_HEADING = 0x8E2530AA8ADA980E;
        public const ulong SET_ENTITY_ALPHA = 0x44A0870B7E92D7C0;
        public const ulong SET_ENTITY_COMPLETELY_DISABLE_COLLISION = 0x9EBC85ED0FFFE51C;
        public const ulong STOP_ENTITY_FIRE = 0x7F0DD2EBBB651AFF;
        public const ulong SET_OBJECT_TEXTURE_VARIATION = 0x971DA0055324D033;
        public const ulong REQUEST_SCRIPT_AUDIO_BANK = 0x2F844A8B08D76685;
        public const ulong PLAY_SOUND_FRONTEND = 0x67C540AA08E4A6F5;
        public const ulong REQUEST_IPL = 0x41B4893843BBDB74;
        public const ulong REMOVE_IPL = 0xEE6C5AD3ECE0A82D;
        public const ulong ON_ENTER_MP = 0x0888C3502DBBEEF5;
        public const ulong ON_ENTER_SP = 0xD7C10C4A637992C9;
        public const ulong SET_ISLAND_HOPPER_ENABLED = 0x9A9D1BA639675CF1;

        // gamemode information
        public string GamemodeName => "Race";
        public string Name => "Race gamemode";
        public string Description => "The original race gamemode rewritten for new GTAServer.core";
        public string Author => "TheIndra, oldnapalm";

        public static GameServer GameServer;
        public static Session Session;
        public static ILogger Logger;

        public static List<Map> Maps;

        private readonly XmlSerializer Serializer = new(typeof(Map));
        private static readonly Random Random = new();
        private static SQLiteConnection Connection;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            Session.State = State.Voting;
            Session.Votes = new Dictionary<Client, string>();
            Session.Players = new List<Player>();

            Logger = GTAServer.Util.LoggerFactory.CreateLogger<Race>();

            GameServer.RegisterCommands<RaceCommands>();

            Maps = Util.GetMaps()?.Select(map => (Map)Serializer.Deserialize(new StreamReader(map))).ToList();

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            GameEvents.OnTick.Add(OnTick);

            InitDB();

            return true;
        }

        private static void OnTick(int tick)
        {
            if (Session.State == State.Voting && GameServer.Clients.Count >= 1)
            {
                Session.NextEvent = DateTime.Now.AddSeconds(15);
                Session.State = State.Preparing;
                Session.Votes = new Dictionary<Client, string>();
                lock (Session.Players)
                    foreach (var player in Session.Players)
                    {
                        RemoveCheckpoint(player);
                        ClearBlips(player);
                        player.CheckpointsPassed = 0;
                    }
                GameServer.SendChatMessageToAll("Starting in 15 seconds, use /vote to vote for a map");
            }

            if (Session.State == State.Preparing && DateTime.Now > Session.NextEvent)
            {
                Session.State = State.Starting;

                // do this after the voting stage since else we might unload parts of the map where the player still is
                if (Session.Map != null)
                    UnloadIpls(Session.Map /* previous map */);

                lock (Session.Players)
                    foreach (var player in Session.Players)
                        ClearWorld(player); // for clients without WorldCleanUpRequest packet support, can be removed on next client update

                GameServer.SendWorldCleanUpToAll();

                var map = Session.Votes.GroupBy(x => x.Value).OrderByDescending(vote => vote.Count()).FirstOrDefault()?.Key ?? Util.GetRandomMap();
                Session.Map = Maps.First(x => x.Name == map);
                GameServer.SendChatMessageToAll("Map: " + map);
                var record = Record(map);
                if (record.Item1 > 0)
                    GameServer.SendChatMessageToAll($"Record: {TimeSpan.FromMilliseconds(record.Item1):m\\:ss\\.ff} by {record.Item2}");
                GameServer.SendChatMessageToAll("Use /respawn to return to the last checkpoint or /leave to leave the race");
                Logger.LogInformation("Starting new race with map " + map);

                if (Session.Map.Ipls != null)
                    foreach (var ipl in Session.Map.Ipls)
                        GameServer.SendNativeCallToAll(REQUEST_IPL, ipl);

                lock (GameServer.Clients)
                    foreach (var client in GameServer.Clients)
                        Join(client);

                var setupPlayers = new Thread((ThreadStart)delegate
                {
                    lock (Session.Players)
                        foreach (var player in Session.Players)
                        {
                            if (player.Client.GameVersion >= 63)
                                GameServer.SendNativeCallToPlayer(player.Client, SET_ISLAND_HOPPER_ENABLED, "HeistIsland", CayoPericoCheck());
                            player.VehicleHash = (int)Session.Map.AvailableVehicles[Random.Next(Session.Map.AvailableVehicles.Length)];
                            GameServer.SendNativeCallToPlayer(player.Client, REQUEST_MODEL, player.VehicleHash);
                        }
                    Thread.Sleep(1000);
                    int spawnPoint = 0;
                    lock (Session.Players)
                        foreach (var player in Session.Players)
                        {
                            if (player.Client.GameVersion < 63 && CayoPericoCheck()) continue;
                            CreateProps(player);
                            AddCheckpoint(player, 0);
                            CreateBlip(player, 0);
                            CreateBlip(player, 1);
                            CreateVehicle(player, spawnPoint, true);
                            spawnPoint++;
                        }
                });
                setupPlayers.Start();

                GameServer.SendNotificationToAll("The race is about to start");
                GameServer.SendNotificationToAll("Get ready");

                var countdown = new Thread((ThreadStart)delegate
                {
                    Thread.Sleep(10000);
                    for (int i = 3; i > 0; i--)
                    {
                        GameServer.SendNotificationToAll($"{i}");
                        Thread.Sleep(1000);
                    }
                    GameServer.SendNotificationToAll("Go!");

                    lock (Session.Players)
                        foreach (var player in Session.Players)
                            GameServer.SendNativeCallToPlayer(player.Client, FREEZE_ENTITY_POSITION, player.Vehicle, false);

                    Session.State = State.Started;
                    Session.RaceStart = Environment.TickCount64;
                });
                countdown.Start();
            }

            if (Session.State == State.Started)
            {
                lock (Session.Players)
                    foreach (var player in Session.Players)
                    {
                        if (player.Client?.Position == null) continue;

                        // if close/at waypoint
                        if (System.Numerics.Vector3.Distance(player.Client.Position.ToVector3(), Session.Map.Checkpoints[player.CheckpointsPassed].ToVector3()) < 15f)
                        {
                            GameServer.SendNativeCallToPlayer(player.Client, PLAY_SOUND_FRONTEND, 0, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");

                            RemoveCheckpoint(player);

                            if (player.RememberedBlips.Count > player.CheckpointsPassed)
                                GameServer.SendNativeCallToPlayer(player.Client, SET_BLIP_ALPHA, player.RememberedBlips[player.CheckpointsPassed], 0);

                            if (Session.Map.Checkpoints.Length > player.CheckpointsPassed + 1)
                            {
                                player.CheckpointsPassed++;
                                AddCheckpoint(player, player.CheckpointsPassed);

                                if (Session.Map.Checkpoints.Length > player.CheckpointsPassed + 1)
                                    CreateBlip(player, player.CheckpointsPassed + 1);
                            }
                            else
                            {
                                Session.State = State.Voting;

                                var time = Environment.TickCount64 - Session.RaceStart;
                                var msg = $"{player.Client.DisplayName} finished in {TimeSpan.FromMilliseconds(time):m\\:ss\\.ff}";
                                var record = Record(Session.Map.Name);
                                if (record.Item1 > 0 && time < record.Item1)
                                    msg += " (new record)";
                                if (Session.Players.Count > 1)
                                    msg += $" ({Wins(player.Client.DisplayName) + 1} wins)";
                                GameServer.SendChatMessageToAll(msg);
                                SaveTime(Session.Map.Name, player.Client.DisplayName, time, Session.Players.Count > 1 ? 1 : 0);
                            }
                        }
                    }
            }
        }

        private static void OnJoin(Client client)
        {
            if (Session.State != State.Voting && Session.State != State.Preparing)
                client.SendMessage("A race has already started, wait for the next round or use /join to join the race");
            else
                client.SendMessage("The race will start soon, use /vote to vote for a map");

            GameServer.SendNativeCallToPlayer(client, REQUEST_SCRIPT_AUDIO_BANK, "HUD_MINI_GAME_SOUNDSET", true);

            if (client.GameVersion >= 63)
            {
                GameServer.SendNativeCallToPlayer(client, ON_ENTER_MP, true);
                GameServer.SetNativeCallOnDisconnectForPlayer(client, "ON_ENTER_SP", ON_ENTER_SP, true);
            }
        }

        private static void OnLeave(Client client)
        {
            Leave(client, true);
        }

        public static void AddCheckpoint(Player player, int i)
        {
            var next = Session.Map.Checkpoints[i];
            GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER",
                DRAW_MARKER, 1, next, new Vector3(), new Vector3(),
                new Vector3() { X = 10f, Y = 10f, Z = 2f },
                241, 247, 57, 180, false, false, 2, false, false, false, false);

            if (Session.Map.Checkpoints.Length > i + 1)
            {
                var pointTo = Session.Map.Checkpoints[i + 1];
                var dir = System.Numerics.Vector3.Normalize(pointTo.ToVector3() - next.ToVector3());
                GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR",
                    DRAW_MARKER, 20, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                    new Vector3() { X = 60f, Y = 0f, Z = 0f }, new Vector3() { X = 4f, Y = 4f, Z = 4f },
                    87, 193, 250, 200, false, false, 2, false, false, false, false);
            }
            else
            {
                var dir = System.Numerics.Vector3.Normalize(next.ToVector3() - Session.Map.Checkpoints[i - 1].ToVector3());
                GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR",
                    DRAW_MARKER, 4, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                    new Vector3() { X = 0f, Y = 0f, Z = 0f }, new Vector3() { X = 4f, Y = 4f, Z = 4f },
                    87, 193, 250, 200, false, false, 2, false, false, false, false);
            }
        }

        public static void RemoveCheckpoint(Player player)
        {
            GameServer.RecallNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER");
            GameServer.RecallNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR");
        }

        public static void CreateVehicle(Player player, int spawnPoint, bool freeze)
        {
            var position = Session.Map.SpawnPoints[spawnPoint % Session.Map.SpawnPoints.Length].Position;
            var heading = Session.Map.SpawnPoints[spawnPoint % Session.Map.SpawnPoints.Length].Heading;
            GameServer.SetPlayerPosition(player.Client, new Vector3(position.X + 4f, position.Y, position.Z));
            GameServer.GetNativeCallFromPlayer(player.Client, "spawn", CREATE_VEHICLE, new IntArgument(),
                delegate (object o)
                {
                    GameServer.SendNativeCallToPlayer(player.Client, SET_PED_INTO_VEHICLE, new LocalPlayerArgument(), (int)o, -1);
                    if (freeze)
                        GameServer.SendNativeCallToPlayer(player.Client, FREEZE_ENTITY_POSITION, (int)o, true);
                    GameServer.SendNativeCallToPlayer(player.Client, SET_MODEL_AS_NO_LONGER_NEEDED, player.VehicleHash);
                    player.Vehicle = (int)o;
                }, player.VehicleHash, position.X, position.Y, position.Z, heading, false, false);
        }

        public static void CreateBlip(Player player, int i)
        {
            var pos = Session.Map.Checkpoints[i];
            GameServer.GetNativeCallFromPlayer(player.Client, $"blip{i}", ADD_BLIP_FOR_COORD, new IntArgument(),
                delegate (object o)
                {
                    player.RememberedBlips.Add((int)o);
                }, pos.X, pos.Y, pos.Z);
        }

        public static void ClearBlips(Player player)
        {
            if (player.RememberedBlips.Count > player.CheckpointsPassed)
            {
                GameServer.SendNativeCallToPlayer(player.Client, SET_BLIP_ALPHA, player.RememberedBlips[player.CheckpointsPassed], 0);

                if (player.RememberedBlips.Count > player.CheckpointsPassed + 1)
                    GameServer.SendNativeCallToPlayer(player.Client, SET_BLIP_ALPHA, player.RememberedBlips[player.CheckpointsPassed + 1], 0);
            }
            player.RememberedBlips.Clear();
        }

        public static void CreateProps(Player player)
        {
            int i = 0;
            foreach (var prop in Session.Map.DecorativeProps)
            {
                GameServer.SendNativeCallToPlayer(player.Client, REQUEST_MODEL, prop.Hash);
                GameServer.GetNativeCallFromPlayer(player.Client, $"prop{i}", CREATE_OBJECT, new IntArgument(),
                    delegate (object o)
                    {
                        GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_COORDS, (int)o,
                            prop.Position.X, prop.Position.Y, prop.Position.Z, 0, 0, 0, 1);
                        GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_ROTATION, (int)o,
                            prop.Rotation.X, prop.Rotation.Y, prop.Rotation.Z, 2, 1);
                        if (prop.Dynamic)
                            GameServer.SendNativeCallToPlayer(player.Client, FREEZE_ENTITY_POSITION, (int)o, true);
                        if (prop.Texture > 0 && prop.Texture < 16)
                            GameServer.SendNativeCallToPlayer(player.Client, SET_OBJECT_TEXTURE_VARIATION, (int)o, prop.Texture);
                        GameServer.SendNativeCallToPlayer(player.Client, SET_MODEL_AS_NO_LONGER_NEEDED, prop.Hash);
                        player.RememberedProps.Add((int)o);
                    }, prop.Hash, prop.Position.X, prop.Position.Y, prop.Position.Z, 1, 1, prop.Dynamic);
                i++;
            }
        }

        // for clients without WorldCleanUpRequest packet support, can be removed on next client update
        public static void ClearWorld(Player player)
        {
            foreach (var prop in player.RememberedProps)
            {
                GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_ALPHA, prop, 0, false);
                GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_COMPLETELY_DISABLE_COLLISION, prop, false, false);
            }
            player.RememberedProps.Clear();

            GameServer.SendNativeCallToPlayer(player.Client, TASK_LEAVE_VEHICLE, new LocalPlayerArgument(), player.Vehicle, 16);
            GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_ALPHA, player.Vehicle, 0, false);
            GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_COMPLETELY_DISABLE_COLLISION, player.Vehicle, false, false);
        }

        public static void Join(Client client)
        {
            lock (Session.Players)
                if (Session.Players.Any(x => x.Client == client))
                    return;

            if (Session.State == State.Started)
            {
                if (client.GameVersion >= 63)
                    GameServer.SendNativeCallToPlayer(client, SET_ISLAND_HOPPER_ENABLED, "HeistIsland", CayoPericoCheck());
                else if (CayoPericoCheck()) return;

                if (Session.Map.Ipls != null)
                    foreach (var ipl in Session.Map.Ipls)
                        GameServer.SendNativeCallToPlayer(client, REQUEST_IPL, ipl);

                var setupPlayer = new Thread((ThreadStart)delegate
                {
                    var player = new Player(client)
                    {
                        VehicleHash = (int)Session.Map.AvailableVehicles[Random.Next(Session.Map.AvailableVehicles.Length)]
                    };
                    GameServer.SendNativeCallToPlayer(client, REQUEST_MODEL, player.VehicleHash);
                    Thread.Sleep(1000);
                    CreateProps(player);
                    AddCheckpoint(player, 0);
                    CreateBlip(player, 0);
                    CreateBlip(player, 1);
                    CreateVehicle(player, Random.Next(Session.Map.SpawnPoints.Length), false);
                    lock (Session.Players)
                        Session.Players.Add(player);
                    GameServer.SendChatMessageToAll($"{client.DisplayName} joined the race");
                });
                setupPlayer.Start();
            }
            else
                lock (Session.Players)
                    Session.Players.Add(new Player(client));
        }

        public static void Leave(Client client, bool disconnected)
        {
            if (Session.Votes.ContainsKey(client))
                Session.Votes.Remove(client);

            Player player;
            lock (Session.Players)
                player = Session.Players.FirstOrDefault(x => x.Client == client);
            if (player != default)
            {
                if (!disconnected)
                {
                    RemoveCheckpoint(player);
                    ClearBlips(player); // for clients without WorldCleanUpRequest packet support, can be removed on next client update
                    ClearWorld(player); // "
                    GameServer.SendWorldCleanUpToPlayer(client);
                    if (Session.Map != null)
                        UnloadIpls(Session.Map, client);
                    GameServer.SendChatMessageToAll($"{client.DisplayName} left the race");
                }

                lock (Session.Players)
                    Session.Players.Remove(player);
            }

            lock (Session.Players)
                if (!Session.Players.Any())
                    Session.State = State.Voting;
        }

        public static void Respawn(Client client)
        {
            Player player;
            lock (Session.Players)
                player = Session.Players.FirstOrDefault(x => x.Client == client);
            if (player != default)
            {
                var last = player.CheckpointsPassed > 0 ? Session.Map.Checkpoints[player.CheckpointsPassed - 1] :
                    Session.Map.SpawnPoints[Random.Next(Session.Map.SpawnPoints.Length)].Position;
                var dir = System.Numerics.Vector3.Normalize(Session.Map.Checkpoints[player.CheckpointsPassed].ToVector3() - last.ToVector3());
                var heading = (float)(-Math.Atan2(dir.X, dir.Y) * 180.0 / Math.PI);
                GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_COORDS, player.Vehicle, last.X, last.Y, last.Z, 0, 0, 0, 1);
                GameServer.SendNativeCallToPlayer(player.Client, SET_ENTITY_HEADING, player.Vehicle, heading);
                GameServer.SendNativeCallToPlayer(player.Client, STOP_ENTITY_FIRE, player.Vehicle);
                GameServer.SendNativeCallToPlayer(player.Client, SET_PED_INTO_VEHICLE, new LocalPlayerArgument(), player.Vehicle, -1);
                GameServer.SendNativeCallToPlayer(player.Client, SET_VEHICLE_FIXED, player.Vehicle);
                GameServer.SendNativeCallToPlayer(player.Client, SET_VEHICLE_ENGINE_ON, player.Vehicle, true, true);
            }
        }

        private static void UnloadIpls(Map map)
        {
            // unload all ipls again
            if (map.Ipls != null)
                foreach (var ipl in map.Ipls)
                    GameServer.SendNativeCallToAll(REMOVE_IPL, ipl);
        }

        private static void UnloadIpls(Map map, Client client)
        {
            if (map.Ipls != null)
                foreach (var ipl in map.Ipls)
                    GameServer.SendNativeCallToPlayer(client, REMOVE_IPL, ipl);
        }

        private static bool CayoPericoCheck()
        {
            return System.Numerics.Vector3.Distance(Session.Map.SpawnPoints[0].Position.ToVector3(), new System.Numerics.Vector3(5031.428f, -5150.907f, 0f)) < 2000f;
        }

        private static void InitDB()
        {
            var filename = Path.Combine(AppContext.BaseDirectory, "Gamemodes", "times.db");

            if (!File.Exists(filename))
                SQLiteConnection.CreateFile(filename);

            var connectionString = new SQLiteConnectionStringBuilder()
            {
                DataSource = filename,
                Version = 3
            };

            Connection = new SQLiteConnection(connectionString.ToString());
            Connection.Open();

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS `times` (
                    `Id` INTEGER PRIMARY KEY AUTOINCREMENT,
                    `Race` TEXT,
                    `Player` TEXT,
                    `Time` INTEGER,
                    `Win` INTEGER
                );"
            , Connection).ExecuteNonQuery();
        }

        private static void SaveTime(string race, string player, long time, int win)
        {
            var query = new SQLiteCommand(
                "INSERT INTO `times` (`Race`, `Player`, `Time`, `Win`) VALUES (@race, @player, @time, @win);", Connection);
            query.Parameters.AddWithValue("@race", race);
            query.Parameters.AddWithValue("@player", player);
            query.Parameters.AddWithValue("@time", time);
            query.Parameters.AddWithValue("@win", win);
            query.ExecuteNonQuery();
        }

        private static Tuple<long, string> Record(string race)
        {
            var query = new SQLiteCommand("SELECT * FROM `times` WHERE `race` = @race ORDER BY `time` ASC LIMIT 1;", Connection);
            query.Parameters.AddWithValue("@race", race);

            Tuple<long, string> record;
            var reader = query.ExecuteReader();
            if (reader.Read())
                record = new((long)reader["Time"], (string)reader["Player"]);
            else
                record = new(0, "");
            reader.Close();

            return record;
        }

        private static int Wins(string player)
        {
            var query = new SQLiteCommand("SELECT COUNT(*) FROM `times` WHERE `player` = @player AND `win` = 1;", Connection);
            query.Parameters.AddWithValue("@player", player);

            return Convert.ToInt32(query.ExecuteScalar());
        }
    }
}
