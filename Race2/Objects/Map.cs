using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;

namespace Race.Objects
{
    public class Map
    {
        public string Name { get; set; }

        public List<Vector3> Waypoints { get; set; }
    }
}
