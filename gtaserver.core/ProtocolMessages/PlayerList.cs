using ProtoBuf;
using System.Collections.Generic;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    class PlayerList
    {
        [ProtoMember(1)]
        public List<PlayerListMember> Members { get; set; } = new List<PlayerListMember>();
    }

    [ProtoContract]
    class PlayerListMember
    {
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public int Latency { get; set; }
        [ProtoMember(4)]
        public byte[] Address { get; set; }
        [ProtoMember(5)]
        public int GameVersion { get; set; }
    }
}