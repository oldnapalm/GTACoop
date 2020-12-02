using Lidgren.Network;
using Prometheus;
using System.IO;
using System.Text;

namespace GTAServer.Logging
{
    public class PrometheusMetrics
    {
        public Gauge SentBytes =
            Metrics.CreateGauge("gtaserver_netstats_sent_bytes", "Number of bytes sent since startup");

        public Gauge ReceivedBytes =
            Metrics.CreateGauge("gtaserver_netstats_received_bytes", "Number of bytes received since startup");

        public Gauge ActiveConnections =
            Metrics.CreateGauge("gtaserver_netstats_active_connections", "Number of active connections");

        public PrometheusMetrics()
        {
            Metrics.SuppressDefaultMetrics();
        }

        public void Tick(GameServer gameServer)
        {
            var stats = gameServer.Server.Statistics;

            SentBytes.Set(stats.SentBytes);
            ReceivedBytes.Set(stats.ReceivedBytes);

            ActiveConnections.Set(gameServer.Server.ConnectionsCount);
        }

        public void HandleConnection(GameServer gameServer, NetIncomingMessage msg, string str)
        {
            using var stream = new MemoryStream();
            Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);

            gameServer.RespondRconMessage(msg.SenderEndPoint, Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}
