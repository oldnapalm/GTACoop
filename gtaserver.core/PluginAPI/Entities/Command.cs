namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Represents a registered command
    /// </summary>
    public class Command
    {
        /// <summary>
        /// Gets or sets the name of the command
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the command
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the usage of the command
        /// </summary>
        public string Usage { get; set; }
    }
}
