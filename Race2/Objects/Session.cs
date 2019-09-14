using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;

namespace Race.Objects
{
    public struct Session
    {
        public State State;
        public Dictionary<Client, string> Votes;
    }
}
