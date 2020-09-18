using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;
using GTAServer.Users.Groups;

namespace GTAServer.PluginAPI
{
    public interface IPermissionProvider
    {
        bool HasPermission(Client client, PermissionType type, string permission);
    }
}
