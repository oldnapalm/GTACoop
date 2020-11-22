using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Attributes;
using GTAServer.PluginAPI.Entities;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;

namespace GTAServer.Commands
{
    class AdminCommands
    {
        [Command("tp")]
        public static void Teleport(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is ConsoleCommandSender)
            {
                ctx.SendMessage("You cannot execute this command as console");
                return;
            }

            var target = ctx.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                ctx.SendMessage("Player not found");
                return;
            }

            ctx.Client.Position = target.Position;
            ctx.SendMessage("Teleported to " + target.DisplayName);
        }

        [Command(("kick"))]
        public static void Kick(CommandContext ctx, List<string> args)
        {
            var target = ctx.GameServer.Clients.Find(x =>
                string.Equals(x.DisplayName.ToLower(), string.Join(" ", args).ToLower(), StringComparison.Ordinal));
            if (target == null)
            {
                ctx.SendMessage("Player not found");
                return;
            }

            target.Kick("Kicked by " + ctx.Sender.DisplayName, false);
        }

        [Command("reload")]
        public static void Reload(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is Client)
            {
                ctx.SendMessage("This command can only be executed as console");
                return;
            }

            if(args.Count == 0 || args[0] != "confirm")
            {
                ctx.SendMessage("WARNING this command will attempt to reload all plugins, this CAN brick the server. use 'reload confirm' to continue");
                return;
            }

            foreach(var plugin in ServerManager.Plugins)
            {
                var instance = plugin.Instance;
                ctx.GameServer.logger.LogTrace("Attempting to unload {}", instance.FullName);

                plugin.Plugin = null;
                FreeLibrary(GetModuleHandle(instance.GetName().Name));

                plugin.Plugin = PluginLoader.LoadPlugin(instance.GetName().Name, out instance)[0];
                plugin.Instance = instance;

                plugin.Plugin.OnEnable(ctx.GameServer, true);
            }
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr hLibModule);
    }
}
