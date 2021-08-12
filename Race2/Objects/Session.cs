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

        public Dictionary<Client, int> Players;
        public Map Map;
    }
}
