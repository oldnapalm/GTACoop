using System.Net;

namespace GTAServer.PluginAPI.Entities
{
    /// <summary>
    /// Command sender representing a remote rcon client
    /// </summary>
    public class RconCommandSender : ConsoleCommandSender
    {
        /// <summary>
        /// Gets the sender destination IP address
        /// </summary>
        public IPEndPoint Destination { get; internal set; }

        /// <summary>
        /// Replies to the sender executing the command
        /// </summary>
        /// <param name="message">The message to send</param>
        public override void SendMessage(string message)
        {
            GameServer.RespondRconMessage(Destination, message);
        }
    }
}
