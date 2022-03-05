namespace GTAServer.PluginAPI
{
    /// <summary>
    /// Base interface for any gamemode, this class has to be inherited for the plugin to be loaded as gamemode
    /// </summary>
    public interface IGamemode : IPlugin
    {
        /// <summary>
        /// Gets the name of the gamemode
        /// </summary>
        string GamemodeName { get; }
    }
}
