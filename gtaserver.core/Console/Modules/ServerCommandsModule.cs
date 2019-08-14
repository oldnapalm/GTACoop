using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                instance.Log($"This server is running GTAServer.core, commit {version}. For more info see gtacoop.com");
            });

            instance.AddCommand("tps", args =>
            {
                instance.Log("TPS: " + ServerManager.GameServer.TicksPerSecond);
            });

            instance.AddCommand("version", args =>
            {
                instance.Log($"You are running commit {version} ({branch})");
            });
        }

        public string Name => "Server commands module";

        public string Description =>
            "Contains helpful console commands which gives info about the server; tps, version build etc";
    }
}
