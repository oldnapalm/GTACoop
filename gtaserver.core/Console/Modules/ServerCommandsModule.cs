using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.Versioning;

namespace GTAServer.Console.Modules
{
    internal class ServerCommandsModule : IModule
    {
        public void OnEnable(ConsoleInstance instance)
        {
            instance.AddCommand("about", args =>
            {
                instance.Log("This server is running GTAServer.core, for more info see gtacoop.com");
            });

            instance.AddCommand("tps", args =>
            {
                instance.Log("TPS: " + ServerManager.GameServer.TicksPerSecond);
            });

            // command showing dotnet version this server is running on will always be 2.2 if ran by publish build
            instance.AddCommand("_dotnet", args =>
            {
                instance.Log("This servers runs on " +
                             Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
            });
        }

        public string Name => "Server commands module";

        public string Description =>
            "Contains helpful console commands which gives info about the server; tps, version build etc";
    }
}
