using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using Race.Commands;
using Race.Objects;

namespace Race
{
    public class Race : IGamemode
    {
        // gamemode information
        public string GamemodeName => "Race";
        public string Name => "Race gamemode";
        public string Description => "The original race gamemode rewritten for new GTAServer.core";
        public string Author => "TheIndra, oldnapalm";

        public static GameServer GameServer;
        public static Session Session;

        public static List<Map> Maps;

        private readonly XmlSerializer Serializer = new(typeof(Map));

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            Session.State = State.Voting;
            Session.Votes = new Dictionary<Client, string>();
            Session.Players = new List<Player>();

            GameServer.RegisterCommands<RaceCommands>();

            Maps = Util.GetMaps()?.Select(map => (Map)Serializer.Deserialize(new StreamReader(map))).ToList();

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            GameEvents.OnTick.Add(OnTick);

            return true;
        }

        private static void OnTick(int tick)
        {
            if (Session.State == State.Voting && GameServer.Clients.Count >= 1)
            {
                Session.State = State.Starting;
                Session.Players = new List<Player>();
                Session.Votes = new Dictionary<Client, string>();
                Session.NextEvent = DateTime.Now.AddSeconds(15);
                GameServer.RecallNativeCallOnTickForAll("RACE_CHECKPOINT_MARKER");
                GameServer.RecallNativeCallOnTickForAll("RACE_CHECKPOINT_MARKER_DIR");
                GameServer.SendChatMessageToAll("Starting in 15 seconds, use /vote to vote for a map");
            }

            if (Session.State == State.Starting && DateTime.Now > Session.NextEvent)
            {
                Session.State = State.Started;
                var map = Session.Votes.GroupBy(x => x.Value).OrderByDescending(vote => vote.Count()).FirstOrDefault()?.Key ?? Util.GetRandomMap();
                Session.Map = Maps.First(x => x.Name == map);
                GameServer.SendChatMessageToAll("Map: " + map + ", use /leave to leave the race");

                lock (GameServer.Clients)
                    for (int i = 0; i < GameServer.Clients.Count; i++)
                        SetUpPlayerForRace(GameServer.Clients[i], i, true);

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
                            GameServer.SendNativeCallToPlayer(player.Client, 0x428CA6DBD1094446, player.Vehicle, false);

                    Session.RaceStart = Environment.TickCount;
                });
                countdown.Start();
            }

            if (Session.State == State.Started)
            {
                lock (Session.Players)
                    foreach (var player in Session.Players)
                    {
                        var current = Session.Map.Checkpoints[player.CheckpointsPassed];
                        // if close/at waypoint
                        if (System.Numerics.Vector3.Distance(player.Client.Position.ToVector3(), current.ToVector3()) < 10)
                        {
                            GameServer.RecallNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER");
                            GameServer.RecallNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR");

                            if (Session.Map.Checkpoints.Length > player.CheckpointsPassed + 1)
                            {
                                var next = Session.Map.Checkpoints[player.CheckpointsPassed + 1];
                                GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER",
                                    0x28477EC23D892089, 1, next, new Vector3(), new Vector3(),
                                    new Vector3() { X = 10f, Y = 10f, Z = 2f },
                                    241, 247, 57, 180, false, false, 2, false, false, false, false);

                                GameServer.SendNativeCallToPlayer(player.Client, 0xFE43368D2AA4F2FC, next.X, next.Y);

                                if (Session.Map.Checkpoints.Length > player.CheckpointsPassed + 2)
                                {
                                    var pointTo = Session.Map.Checkpoints[player.CheckpointsPassed + 2];
                                    var dir = System.Numerics.Vector3.Normalize(pointTo.ToVector3() - next.ToVector3());
                                    GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR",
                                        0x28477EC23D892089, 20, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                                        new Vector3() { X = 60f, Y = 0f, Z = 0f }, new Vector3() { X = 4f, Y = 4f, Z = 4f },
                                        87, 193, 250, 200, false, false, 2, false, false, false, false);
                                }
                                else
                                {
                                    var dir = System.Numerics.Vector3.Normalize(next.ToVector3() - current.ToVector3());
                                    GameServer.SetNativeCallOnTickForPlayer(player.Client, "RACE_CHECKPOINT_MARKER_DIR",
                                        0x28477EC23D892089, 4, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                                        new Vector3() { X = 0f, Y = 0f, Z = 0f }, new Vector3() { X = 4f, Y = 4f, Z = 4f },
                                        87, 193, 250, 200, false, false, 2, false, false, false, false);
                                }

                                player.CheckpointsPassed++;
                            }
                            else
                            {
                                GameServer.SendNotificationToAll($"~y~{player.Client.DisplayName} ~s~finished the race!");
                                GameServer.SendNotificationToAll($"Time: {TimeSpan.FromSeconds((Environment.TickCount - Session.RaceStart) / 1000):mm\\:ss}");

                                Session.State = State.Voting;
                            }
                        }
                    }

                if (!GameServer.Clients.Any())
                    Session.State = State.Voting;
            }
        }

        private static void OnJoin(Client client)
        {
            if (Session.State != State.Voting && Session.State != State.Starting)
                client.SendMessage("A race has already started, wait for the next round or use /join to join the race");
            else
                client.SendMessage("The race will start soon, use /vote to vote for a map");
        }

        private static void OnLeave(Client client)
        {
            Leave(client);
        }

        private static void SetUpPlayerForRace(Client client, int spawnPoint, bool freeze)
        {
            var createVehicle = new Thread((ThreadStart)delegate
            {
                var position = Session.Map.SpawnPoints[spawnPoint % Session.Map.SpawnPoints.Length].Position;
                var heading = Session.Map.SpawnPoints[spawnPoint % Session.Map.SpawnPoints.Length].Heading;
                var model = (int)Session.Map.AvailableVehicles[new Random().Next(Session.Map.AvailableVehicles.Length)];
                GameServer.SetPlayerPosition(client, position);
                GameServer.SetNativeCallOnTickForPlayer(client, "RACE_REQUEST_MODEL", 0x963D27A58DF860AC, model);
                Thread.Sleep(5000);
                GameServer.RecallNativeCallOnTickForPlayer(client, "RACE_REQUEST_MODEL");
                GameServer.GetNativeCallFromPlayer(client, "spawn", 0xAF35D0D2583051B0, new IntArgument(),
                    delegate (object o)
                    {
                        GameServer.SendNativeCallToPlayer(client, 0xF75B0D629E1C063D, new LocalPlayerArgument(), (int)o, -1);
                        if (freeze)
                            GameServer.SendNativeCallToPlayer(client, 0x428CA6DBD1094446, (int)o, true);
                        GameServer.SendNativeCallToPlayer(client, 0xE532F5D78798DAAB, model);
                        lock (Session.Players)
                            Session.Players.Add(new Player(client, (int)o));
                    }, model, position.X, position.Y, position.Z, heading, false, false);
            });
            createVehicle.Start();

            var next = Session.Map.Checkpoints[0];
            GameServer.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER",
                0x28477EC23D892089, 1, next, new Vector3(), new Vector3(),
                new Vector3() { X = 10f, Y = 10f, Z = 2f },
                241, 247, 57, 180, false, false, 2, false, false, false, false);

            var pointTo = Session.Map.Checkpoints[1];
            var dir = System.Numerics.Vector3.Normalize(pointTo.ToVector3() - next.ToVector3());
            GameServer.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER_DIR",
                0x28477EC23D892089, 20, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                new Vector3() { X = 60f, Y = 0f, Z = 0f }, new Vector3() { X = 4f, Y = 4f, Z = 4f },
                87, 193, 250, 200, false, false, 2, false, false, false, false);

            GameServer.SendNativeCallToPlayer(client, 0xFE43368D2AA4F2FC, next.X, next.Y);
        }

        public static void Join(Client client)
        {
            SetUpPlayerForRace(client, 0, false);
        }

        public static void Leave(Client client)
        {
            if (Session.Votes.ContainsKey(client))
                Session.Votes.Remove(client);

            lock (Session.Players)
            {
                Player toRemove = Session.Players.FirstOrDefault(x => x.Client == client);
                if (toRemove != default)
                    Session.Players.Remove(toRemove);
            }

            GameServer.RecallNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER");
            GameServer.RecallNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER_DIR");

            if (!Session.Players.Any())
                Session.State = State.Voting;
        }
    }
}
