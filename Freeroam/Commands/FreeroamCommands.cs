using System.Threading;
using System.Threading.Tasks;
using GTAServer.PluginAPI.Entities;
using GTAServer.PluginAPI.Attributes;
using GTAServer.ProtocolMessages;
using System.Collections.Generic;

namespace GTAServer.Commands
{
    class FreeroamCommands
    {
        [Command("respawn", Description = "Respawns player", Usage = "Usage: respawn")]
        public static void Respawn(CommandContext ctx, List<string> args)
        {
            ctx.GameServer.SendNotificationToAll($"~g~{ctx.Client.DisplayName} ~s~respawned");

            ctx.Client.SendNativeCall(0x891B5B39AC6302AF, 500); // DO_SCREEN_FADE_OUT

            Task.Run(() =>
            {
                Thread.Sleep(500);
                
                ctx.Client.SendNativeCall(0xAAA34F8A7CB32098, new LocalPlayerArgument()); // CLEAR_PED_TASKS_IMMEDIATELY
                ctx.GameServer.SetPlayerPosition(ctx.Client, Freeroam.Freeroam.Configuration.SpawnCoordinates);

                ctx.Client.SendNativeCall(0xD4E8E24955024033, 500); // DO_SCREEN_FADE_IN
            });
        }

        [Command("car", Description = "Spawns a car", Usage = "Usage: car")]
        public static void SpawnCar(CommandContext ctx, List<string> args)
        {
            ctx.Client.SendNativeCall(0xAF35D0D2583051B0, -344943009, ctx.Client.Position.X, ctx.Client.Position.Y, ctx.Client.Position.Z, true, true);
        }
    }
}
