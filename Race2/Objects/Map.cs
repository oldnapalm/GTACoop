using GTAServer.ProtocolMessages;

namespace Race.Objects
{
    public class Map
    {
        public Vector3[] Checkpoints;
        public SpawnPoint[] SpawnPoints;
        public VehicleHash[] AvailableVehicles;
        public string[] Ipls;

        public string Description;
        public string Name;

        public Map() { }
    }

    public class SpawnPoint
    {
        public Vector3 Position;
        public float Heading;
    }
}
