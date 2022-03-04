namespace GTAServer.PluginAPI.Entities
{
    public class PluginMessage
    {
        /// <summary>
        /// Gets the name of the plugin message
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the data of the plugin message
        /// </summary>
        public byte[] Data { get; internal set; }
    }
}
