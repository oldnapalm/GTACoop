namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Represents a permission type
    /// </summary>
    public enum PermissionType
    {
        /// <summary>
        /// The permission is for a command
        /// </summary>
        Command,

        /// <summary>
        /// The permission is a regular permission
        /// </summary>
        Permission,

        /// <summary>
        /// The permission inherits all permission from a group
        /// </summary>
        Group
    }
}
