namespace GTAServer.PluginAPI
{
    /// <summary>
    /// Base interface for any plugin, all classes inheriting this will be loaded as plugin
    /// </summary>
    public interface IPlugin 
    {
        /// <summary>
        /// Gets the name of the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the plugin
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the name of the plugin author.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Plugin entry point, called when a plugin is being enabled.
        /// Use this to register any necessary hooks and commands.
        /// </summary>
        /// <param name="gameServer">Game server object.</param>
        /// <param name="isAfterServerLoad">If the plugin is being started after the server has started.</param>
        /// <returns>Whether the plugin successfully loaded</returns>
        bool OnEnable(GameServer gameServer, bool isAfterServerLoad);   
    }
}
