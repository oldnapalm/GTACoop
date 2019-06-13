using GTAServer.ProtocolMessages;
using System.Collections.Generic;

namespace Race.Objects
{
    class RaceObject
    {
        public string Name { get; set; }
        public string Description { get; set; }

        // 1st = start, last = finish
        public List<SpawnPoint> SpawnPoints { get; set; }
    }

    class SpawnPoint
    {
        public Vector3 Position { get; set; }
        public double Heading { get; set; }
    }
}
