using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;

namespace GTAServer.PluginAPI.Events
{
    public static class ConnectionEvents
    {
        /// <summary>
        /// Called whenever a new connection request comes in.
        /// </summary>
        public static List<Func<Client, ConnectionRequest, PluginResponse<ConnectionRequest>>> OnConnectionRequest
            = new List<Func<Client, ConnectionRequest, PluginResponse<ConnectionRequest>>>();

        /// <summary>
        /// Internal method. Triggers OnConnectionRequest
        /// </summary>
        /// <param name="c">Client who the request is from</param>
        /// <param name="r">Connection request</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received message.</returns>
        public static PluginResponse<ConnectionRequest> ConnectionRequest(Client c, ConnectionRequest r)
        {
            var result = new PluginResponse<ConnectionRequest>()
            {
                ContinueServerProc = true,
                ContinuePluginProc = true,
                Data = r
            };
            foreach (var f in OnConnectionRequest)
            {
                result = f(c, r);
                if (!result.ContinuePluginProc) return result;
                r = result.Data;
            }

            return result;
        }

        /// <summary>
        /// Called whenever a client has joined the server.
        /// </summary>
        public static List<Action<Client>> OnJoin = new List<Action<Client>>();
        /// <summary>
        /// Internal method. Triggers OnJoin.
        /// </summary>
        /// <param name="c">Client who joined the server</param>
        internal static void Join(Client c) => OnJoin.ForEach(f => f(c));
    }
}