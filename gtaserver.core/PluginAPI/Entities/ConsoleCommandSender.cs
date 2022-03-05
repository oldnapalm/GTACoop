using Microsoft.Extensions.Logging;

namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Command sender representing the console
    /// </summary>
    public class ConsoleCommandSender : ICommandSender
    {
        /// <summary>
        /// Gets the name of the sender
        /// </summary>
        public string DisplayName { get; } = "Console";

        /// <summary>
        /// Gets the console <see cref="GTAServer.GameServer"/> instances
        /// </summary>
        public GameServer GameServer { get; internal set; }

        /// <summary>
        /// Replies to the sender executing the command
        /// </summary>
        /// <param name="message">The message to send</param>
        public virtual void SendMessage(string message)
        {
            GameServer.logger.LogInformation(message);
        }
    }
}
