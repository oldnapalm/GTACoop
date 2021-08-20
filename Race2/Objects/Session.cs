using System;
using System.Collections.Generic;
using GTAServer.ProtocolMessages;

namespace Race.Objects
{
    public struct Session
    {
        public State State;
        public Dictionary<Client, string> Votes;
        public DateTime NextEvent;

        public List<Player> Players;
        public Map Map;
        public int RaceStart;
        public int Vehicle;
    }

    public class Player
    {
        public Client Client;
        public int Vehicle;
        public int CheckpointsPassed;

        public Player(Client client, int vehicle)
        {
            Client = client;
            Vehicle = vehicle;
            CheckpointsPassed = 0;
        }
    }
}
