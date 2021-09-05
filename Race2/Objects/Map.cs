using GTAServer.ProtocolMessages;
using System.Xml.Serialization;

namespace Race.Objects
{
    [XmlRoot(ElementName = "Race")]
    public class Map
    {
        public Vector3[] Checkpoints;
        public SpawnPoint[] SpawnPoints;
        public VehicleHash[] AvailableVehicles;
        public SavedProp[] DecorativeProps;
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

    public class SavedProp
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public int Hash;
        public bool Dynamic;
    }
}
