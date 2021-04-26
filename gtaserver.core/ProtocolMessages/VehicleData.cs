using System.Collections.Generic;
using System.Numerics;
using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class VehicleData
    {
        [ProtoMember(1)]
        public long Id { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public int VehicleModelHash { get; set; }
        [ProtoMember(4)]
        public int PedModelHash { get; set; }
        [ProtoMember(5)]
        public int PrimaryColor { get; set; }
        [ProtoMember(6)]
        public int SecondaryColor { get; set; }
        [ProtoMember(7)]
        public Vector3 Position { get; set; }
        [ProtoMember(8)]
        public Vector3 Quaternion { get; set; }
        [ProtoMember(9)]
        public int VehicleSeat { get; set; }
        [ProtoMember(10)]
        public int VehicleHealth { get; set; }
        [ProtoMember(11)]
        public int PlayerHealth { get; set; }
        [ProtoMember(12)]
        public float Latency { get; set; }
        [ProtoMember(13)]
        public Dictionary<int, int> VehicleMods { get; set; }
        [ProtoMember(14)]
        public bool IsPressingHorn { get; set; }
        [ProtoMember(15)]
        public bool IsSirenActive { get; set; }
        [ProtoMember(16)]
        public float Speed { get; set; }
        [ProtoMember(17)]
        public bool IsEngineRunning { get; set; }
        [ProtoMember(18)]
        public float WheelSpeed { get; set; }
        [ProtoMember(19)]
        public float Steering { get; set; }
        [ProtoMember(20)]
        public int RadioStation { get; set; }
        [ProtoMember(21)]
        public string Plate { get; set; }
        [ProtoMember(22)]
        public Vector3 Velocity { get; set; }
        [ProtoMember(23)]
        public Dictionary<int, int> PedProps { get; set; }
    }
}
