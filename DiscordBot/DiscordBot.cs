using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;

namespace DiscordBot
{
    public class DiscordSettings
    {
        public string Token { get; set; }
        public string Webhook { get; set; }
        public UInt64 Channel { get; set; }
        public string WelcomeMessage { get; set; }
        public string HelpMessage { get; set; }

        public DiscordSettings()
        {
            Token = "token";
            Webhook = "webhook URL";
            Channel = 0;
            WelcomeMessage = "";
            HelpMessage = "";
        }

        public static DiscordSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(DiscordSettings));
            DiscordSettings settings = null;
            if (File.Exists(path))
                using (var stream = File.OpenRead(path)) settings = (DiscordSettings)ser.Deserialize(stream);
            else
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new DiscordSettings());
            return settings;
        }
    }

    public class DiscordBot : IPlugin
    {
        public string Name => "Discord Bot";
        public string Description => "Discord bridge plugin using Discord.Net";
        public string Author => "oldnapalm";
        private GameServer _server;
        private DiscordSettings _settings;
        private DiscordSocketClient _client;
        private DiscordWebhookClient _webhook;
        private IMessageChannel _channel;

        private void OnJoin(Client c)
        {
            SendToDiscord(c.DisplayName + " connected", "Server").GetAwaiter().GetResult();
        }

        private void OnDisconnect(Client c)
        {
            SendToDiscord(c.DisplayName + " disconnected", "Server").GetAwaiter().GetResult();
        }

        private PluginResponse<ChatData> OnChatMessage(Client c, ChatData d)
        {
            if (!d.Message.StartsWith("/"))
                SendToDiscord(d.Message, c.DisplayName).GetAwaiter().GetResult();
            return new PluginResponse<ChatData>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = d
            };
        }

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            _server = gameServer;
            string configFile = "Configuration" + Path.DirectorySeparatorChar + "discordSettings.xml";
            _settings = DiscordSettings.ReadSettings(configFile);
            if (_settings.Token != "token" && _settings.Webhook != "webhook URL" && _settings.Channel != 0)
            {
                MainAsync().GetAwaiter().GetResult();
                _webhook = new DiscordWebhookClient(_settings.Webhook);
            }
            else
            {
                Console.WriteLine("Edit Discord settings in " + configFile);
                return false;
            }
            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnDisconnect);
            GameEvents.OnChatMessage.Add(OnChatMessage);
            return true;
        }

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
            if (_settings.WelcomeMessage.Length > 0)
                _client.UserJoined += WelcomeJoinedUser;
            await _client.LoginAsync(TokenType.Bot, _settings.Token);
            await _client.StartAsync();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            _channel = (IMessageChannel)_client.GetChannel(_settings.Channel);
            Console.WriteLine($"{_client.CurrentUser} is connected");
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Author.Id == _client.CurrentUser.Id)
                return;
            if (message.Content == "!ping")
                await message.Channel.SendMessageAsync("pong");
            else if (message.Content == "!help" && _settings.HelpMessage.Length > 0)
                await message.Channel.SendMessageAsync(_settings.HelpMessage);
            else if (message.Channel == _channel && !message.Author.IsBot && message.Content.Length > 0)
                _server.SendChatMessageToAll(message.Author.Username + " [Discord]: " + message.Content);
        }

        public async Task WelcomeJoinedUser(SocketUser user)
        {
            await _channel.SendMessageAsync($"{user.Mention}\n" + _settings.WelcomeMessage);
        }

        public async Task SendToDiscord(String message, String name)
        {
            await _webhook.SendMessageAsync(text: message, username: name);
        }
    }
}
