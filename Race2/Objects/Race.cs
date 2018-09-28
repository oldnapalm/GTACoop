using GTAServer.ProtocolMessages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Race.Objects
{
    class Race
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public List<SpawnPoint> SpawnPoints { get; set; }
    }

    class SpawnPoint
    {
        public Vector3 Position { get; set; }
        public double Heading { get; set; }
    }
}
