using GTAServer.PluginAPI.Entities;
using GTAServer.ProtocolMessages;

namespace GTAServer.PluginAPI
{
    /// <summary>
    /// Base interface to be inherited by a class handling permissions
    /// </summary>
    public interface IPermissionProvider
    {
        /// <summary>
        /// Returns if the <see cref="Client"/> has the provided permissions
        /// </summary>
        /// <param name="type">The permission type</param>
        /// <param name="permission">The permission</param>
        /// <returns></returns>
        bool HasPermission(Client client, PermissionType type, string permission);
    }
}
