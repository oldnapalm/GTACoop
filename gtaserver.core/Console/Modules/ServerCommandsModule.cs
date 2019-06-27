using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GTAServer.Console.Modules
{
    class ServerCommandsModule : IModule
    {
        public void OnEnable(ConsoleInstance instance)
        {
            string version = "Unknown", branch = "Unknown";
            if (File.Exists("version"))
            {
                VersionModule.ReadVersion(out branch);
            }

            instance.AddCommand("about", args =>
            {
                instance.WriteLn($"This server is running GTAServer.core, commit {version}. For more info see gtacoop.com");
            });

            instance.AddCommand("tps", args =>
            {
                instance.WriteLn("TPS: " + ServerManager.GameServer.TicksPerSecond);
            });

            instance.AddCommand("version", args =>
            {
                instance.WriteLn($"You are running commit {version} ({branch})");
            });
        }
    }
}
