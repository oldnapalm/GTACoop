using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;
using GTAServer.Users.Groups;

namespace GTAServer.Users
{
    public interface IPermissionProvider
    {
        bool HasPermission(Client client, PermissionType type, string permission);
    }
}
