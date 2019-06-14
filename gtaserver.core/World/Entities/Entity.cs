using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;

namespace GTAServer.World.Entities
{
    public class Entity
    {
        private static int _handle;
        private static Client _owner;

        public Entity(int handle, Client owner)
        {
            _handle = handle;
            _owner = owner;
        }

        public void FreezeEntity(bool freeze = true)
        {
            _owner.SendNativeCall(0x428CA6DBD1094446, freeze);
        }
    }
}
