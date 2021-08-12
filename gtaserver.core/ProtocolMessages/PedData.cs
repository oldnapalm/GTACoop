using System;
using System.Collections.Generic;
using ProtoBuf;

namespace GTAServer.ProtocolMessages {
    [Flags]
    public enum PedDataFlags
    {
        IsJumping = 1 << 0,
        IsShooting = 1 << 1,
        IsAiming = 1 << 2,
        IsParachuteOpen = 1 << 3,
        IsInParachuteFreeFall = 1 << 4
    }

    [ProtoContract]
    public class PedData
    {
        [ProtoMember(1)]
        public long Id { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public int PedModelHash { get; set; }
        [ProtoMember(4)]
        public Vector3 Position { get; set; }
        [ProtoMember(5)]
        public Vector3 Quaternion { get; set; }
        [ProtoMember(6)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(7)]
        public int WeaponHash { get; set; }
        [ProtoMember(8)]
        public int PlayerHealth { get; set; }
        [ProtoMember(9)]
        public float Latency { get; set; }
        [ProtoMember(10)]
        public Dictionary<int, int> PedProps { get; set; }
        [ProtoMember(11)]
        public byte? Flag { get; set; }
    }
}