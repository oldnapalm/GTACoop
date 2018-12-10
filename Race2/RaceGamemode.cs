using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using GTAServer;
using GTAServer.PluginAPI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Race
{
    public class RaceGamemode : IGamemode
    {

        public string GamemodeName => "Race";

        public string Name => "Race";
        public string Description => "The official race gamemode";
        public string Author => "TheIndra";

        private static ILogger logger;
        private static List<Objects.Race> _races;

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            logger = Util.LoggerFactory.CreateLogger<RaceGamemode>();
            _races = new List<Objects.Race>();

            if (!Directory.Exists("Races"))
            {
                // races dir doesn't exist so there shouldn't be any races so exit
                logger.LogError("Folder 'Races' not found creating and exiting");

                Directory.CreateDirectory("Races");

                return false;
            }

            if(Directory.GetFiles("Races", "*.json").Length < 1)
            {
                // races dir is empty so there shouldn't be any races so exit
                logger.LogError("No races found in folder 'Races' exiting");

                return false;
            }

            foreach(string file in Directory.GetFiles("Races", "*.json"))
            {
                try
                {
                    _races.Add(JsonConvert.DeserializeObject<Objects.Race>(File.ReadAllText(file)));
                }catch(Exception e)
                {
                    logger.LogError("Couldn't load race " + file + " because of " + e.Message);
                }
            }

            if(_races.Count < 1)
            {
                logger.LogError("Unable to load any races exiting");

                return false;
            }

            logger.LogInformation("Starting main race thread");

            new Thread(() => new Race()).Start();

            return true;
        }
    }
}
