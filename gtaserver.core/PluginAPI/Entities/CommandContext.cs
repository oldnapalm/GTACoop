using GTAServer.ProtocolMessages;

namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Represents a context of an executed command
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// Gets the current gameserver instance
        /// </summary>
        public GameServer GameServer { get; internal set; }

        /// <summary>
        /// Gets the command sender
        /// </summary>
        public ICommandSender Sender { get; internal set; }

        /// <summary>
        /// Gets the client which executed the command
        /// </summary>
        public Client Client { get; internal set; }

        /// <summary>
        /// Gets the chatdata associated with the command
        /// </summary>
        public ChatData ChatData { get; internal set; }

        /// <summary>
        /// Replies to the sender executing the command
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendMessage(string message)
        {
            Sender?.SendMessage(message);
        }
    }
}
