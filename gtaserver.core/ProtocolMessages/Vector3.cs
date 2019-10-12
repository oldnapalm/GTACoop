using System.Numerics;
using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class Vector3
    {
        public Vector3()
        {
            X = 0f;
            Y = 0f;
            Z = 0f;
        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Converts to <see cref="System.Numerics.Vector3"/>
        /// </summary>
        public System.Numerics.Vector3 ToVector3()
        {
            return new System.Numerics.Vector3(X, Y, Z);
        }

        /// <summary>
        /// X
        /// </summary>
        [ProtoMember(1)]
        public float X { get; set; }
        /// <summary>
        /// Y
        /// </summary>
        [ProtoMember(2)]
        public float Y { get; set; }
        /// <summary>
        /// Z
        /// </summary>
        [ProtoMember(3)]
        public float Z { get; set; }
    }
}
