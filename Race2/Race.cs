using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public string Author => "TheIndra";

        public static GameServer GameServer;
        public static Session Session;

        public static List<Map> Maps;

        private readonly XmlSerializer Serializer = new(typeof(Map));

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            Session.State = State.Voting;
            Session.Votes = new Dictionary<Client, string>();
            Session.Players = new Dictionary<Client, int>();

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
                Session.Players = new Dictionary<Client, int>();
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

                int i = 0;
                foreach (var client in GameServer.Clients)
                {
                    var position = Session.Map.SpawnPoints[i % Session.Map.SpawnPoints.Length].Position;
                    var heading = Session.Map.SpawnPoints[i % Session.Map.SpawnPoints.Length].Heading;
                    var model = (int)Session.Map.AvailableVehicles[new Random().Next(Session.Map.AvailableVehicles.Length)];
                    i++;
                    GameServer.SetPlayerPosition(client, position);
                    GameServer.SendNativeCallToPlayer(client, 0x963D27A58DF860AC, model);
                    GameServer.GetNativeCallFromPlayer(client, "spawn", 0xAF35D0D2583051B0, new IntArgument(),
                        delegate (object o)
                        {
                            GameServer.SendNativeCallToPlayer(client, 0xF75B0D629E1C063D, new LocalPlayerArgument(), (int)o, -1);
                            GameServer.SendNativeCallToPlayer(client, 0xE532F5D78798DAAB, model);
                        }, model, position.X, position.Y, position.Z, heading, false, false);

                    Session.Players.Add(client, 0);
                }

                var next = Session.Map.Checkpoints[0];
                GameServer.SetNativeCallOnTickForAll("RACE_CHECKPOINT_MARKER",
                    0x28477EC23D892089, 1, next, new Vector3(), new Vector3(),
                    new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false, false, false);

                var pointTo = Session.Map.Checkpoints[1];
                var dir = System.Numerics.Vector3.Normalize(pointTo.ToVector3() - next.ToVector3());
                GameServer.SetNativeCallOnTickForAll("RACE_CHECKPOINT_MARKER_DIR",
                    0x28477EC23D892089, 20, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                    new Vector3() { X = 60f, Y = 0f, Z = 0f },
                    new Vector3() { X = 4f, Y = 4f, Z = 4f }, 87, 193, 250, 200, false, false, 2, false, false, false, false);

                GameServer.SendNativeCallToAll(0xFE43368D2AA4F2FC, next.X, next.Y);
            }

            if (Session.State == State.Started)
            {
                foreach (var client in Session.Players)
                {
                    var current = Session.Map.Checkpoints[client.Value];
                    // if close/at waypoint
                    if (System.Numerics.Vector3.Distance(client.Key.Position.ToVector3(), current.ToVector3()) < 10)
                    {
                        GameServer.RecallNativeCallOnTickForPlayer(client.Key, "RACE_CHECKPOINT_MARKER");
                        GameServer.RecallNativeCallOnTickForPlayer(client.Key, "RACE_CHECKPOINT_MARKER_DIR");

                        if (Session.Map.Checkpoints.Length > client.Value + 1)
                        {
                            var next = Session.Map.Checkpoints[client.Value + 1];
                            GameServer.SetNativeCallOnTickForPlayer(client.Key, "RACE_CHECKPOINT_MARKER",
                                0x28477EC23D892089, 1, next, new Vector3(), new Vector3(),
                                new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false, false, false);

                            GameServer.SendNativeCallToPlayer(client.Key, 0xFE43368D2AA4F2FC, next.X, next.Y);

                            if (Session.Map.Checkpoints.Length > client.Value + 2)
                            {
                                var pointTo = Session.Map.Checkpoints[client.Value + 2];
                                var dir = System.Numerics.Vector3.Normalize(pointTo.ToVector3() - next.ToVector3());
                                GameServer.SetNativeCallOnTickForPlayer(client.Key, "RACE_CHECKPOINT_MARKER_DIR",
                                    0x28477EC23D892089, 20, next.X, next.Y, next.Z + 2f, dir.X, dir.Y, dir.Z,
                                    new Vector3() { X = 60f, Y = 0f, Z = 0f },
                                    new Vector3() { X = 4f, Y = 4f, Z = 4f }, 87, 193, 250, 200, false, false, 2, false, false, false, false);
                            }

                            Session.Players[client.Key]++;
                        }
                        else
                        {
                            GameServer.SendNotificationToPlayer(client.Key, "Finish!");
                            GameServer.SendNotificationToAll($"~y~{client.Key.DisplayName} ~s~finished the race!");

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
                client.SendMessage("A race has already started, wait for the next round to start");
            else
                client.SendMessage("The race will start soon, use /vote to vote for a map");
        }

        private static void OnLeave(Client client)
        {
            Leave(client);
        }

        public static void Leave(Client client)
        {
            if (Session.Votes.ContainsKey(client))
                Session.Votes.Remove(client);

            if (Session.Players.ContainsKey(client))
                Session.Players.Remove(client);

            GameServer.RecallNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER");
            GameServer.RecallNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER_DIR");

            if (!Session.Players.Any())
                Session.State = State.Voting;
        }
    }
}
