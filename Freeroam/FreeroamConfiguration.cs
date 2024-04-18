using GTAServer.ProtocolMessages;

namespace Freeroam
{
    public class FreeroamConfiguration
    {
        public Vector3 SpawnCoordinates { get; set; } = new Vector3(0f, 0f, 70f);
        public bool SpawnAtSpawn { get; set; } = false;
    }
}
