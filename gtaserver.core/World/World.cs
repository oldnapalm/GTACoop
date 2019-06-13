using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;

namespace GTAServer.World
{
    public class World
    {
        private readonly GameServer _gameServer;

        private int lastIndex = 0;
        private readonly ILogger _logger;

        public World(GameServer gameServer)
        {
            _gameServer = gameServer;
            _logger = _logger = Util.LoggerFactory.CreateLogger<World>();
        }

        public void CreateVehicle(int model, Vector3 position, bool engineRunning = true)
        {
            var vehicle = new VehicleData()
            {
                Name = "Vehicle " + lastIndex++,
                Position = position,
                IsEngineRunning = engineRunning,
                VehicleModelHash = model,
                Id = 0,
                Quaternion = new Quaternion(),
                PedModelHash = (int) 1498487404u
            };

            _gameServer.SendToAll(vehicle, PacketType.NpcVehPositionData, false);
            _logger.LogDebug("Created " + vehicle.Name);
        }

        public void CreateVehicleWithPlayer(int model, Vector3 position, int heading, Client player, int seat = -1)
        {
            _gameServer.GetNativeCallFromPlayer(player, "spawn", 0xAF35D0D2583051B0, new IntArgument(), (vehid) =>
            {
                player.SendNativeCall(0xF75B0D629E1C063D, new LocalPlayerArgument(), vehid, seat);
            }, model, position.X, position.Y, position.Z, heading, false, false);
        }
    }
}
