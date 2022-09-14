namespace GTAServer
{
    public class ServerStatistics
    {
        /// <summary>
        /// Gets the total connections made to the server excluding discovery
        /// </summary>
        public int TotalConnections { get; internal set; }
    }
}
