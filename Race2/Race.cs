using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Race.Commands;
using Race.Objects;

namespace Race
{
    public class Race : IGamemode
    {
        // gamemode information
        public string GamemodeName => "Race";
        public string Name => "Race gamemode";
        public string Description
            => "The orginal race gamemode rewritten for new GTAServer.core";
        public string Author => "TheIndra";

        public static GameServer GameServer;
        public static Session Session;

        public static List<Map> Maps;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            GameServer = gameServer;

            Session.State = State.Voting;
            Session.Votes = new Dictionary<Client, string>();
            Session.Players = new Dictionary<Client, int>();

            GameServer.RegisterCommands<RaceCommands>();

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            GameEvents.OnTick.Add(OnTick);

            return true;
        }

        private void OnTick(int tick)
        {
            if(Session.State == State.Voting && GameServer.Clients.Count() >= 1)
            {
                Session.State = State.Starting;
                Session.NextEvent = DateTime.Now.AddSeconds(15);

                GameServer.SendChatMessageToAll("Starting in 15 seconds...");
            }

            if (Session.State == State.Starting && DateTime.Now > Session.NextEvent)
            {
                Session.State = State.Started;

                // TODO: get most voted map or random
                // TODO: teleport everyone and further stuff
            }

            if (Session.State == State.Started)
            {
                foreach (var client in Session.Players)
                {
                    var nextWaypoint = Session.Map.Waypoints[client.Value + 1];

                    // if close/at waypoint
                    if (System.Numerics.Vector3.Distance(client.Key.Position.ToVector3(), nextWaypoint.ToVector3()) < 5)
                    {
                        Session.Players[client.Key]++;

                        if (Session.Players[client.Key] == Session.Map.Waypoints.Count)
                        {
                            GameServer.SendNotificationToPlayer(client.Key, "Finish!");
                            GameServer.SendNotificationToAll($"~y~{client.Key.DisplayName} ~s~finished the race!");
                        }
                        else
                        {
                            GameServer.SendNotificationToPlayer(client.Key, $"{Session.Players[client.Key]}/{Session.Map.Waypoints.Count}");
                        }
                    }
                }
            }
        }

        public void OnJoin(Client client)
        {
            if (Session.State != State.Voting && Session.State != State.Starting)
            {
                client.SendMessage("A race has already started, wait for the next round to start");
            }
            else
            {
                client.SendMessage("The race will start soon, use /vote to vote for a map");
            }
        }

        private void OnLeave(Client client)
        {
            if (!GameServer.Clients.Any())
            {
                // reset if everyone left
                Session.State = State.Voting;
                Session.Votes = new Dictionary<Client, string>();
                Session.Players = new Dictionary<Client, int>();
            }

            if(Session.Votes.ContainsKey(client))
                Session.Votes.Remove(client);

            if (Session.Players.ContainsKey(client))
                Session.Players.Remove(client);
        }
    }
}
