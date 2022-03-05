namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Represents a sender of the command, this can be a client but also console
    /// </summary>
    public interface ICommandSender
    {
        /// <summary>
        /// Gets the name of the sender
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Replies to the sender executing the command
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendMessage(string message);
    }
}
