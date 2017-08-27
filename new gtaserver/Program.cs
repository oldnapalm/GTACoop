using Newtonsoft.Json;
using System;
using System.IO;
using System.Xml.Serialization;

namespace GTAServer
{
    public class Program
    {

        private static Config _configuration;

        private static readonly String Pluginsfolder = System.AppContext.BaseDirectory +
            Path.DirectorySeparatorChar + "configuration";
        private static readonly String Configurationfolder = System.AppContext.BaseDirectory +
            Path.DirectorySeparatorChar + "plugins";

        static void Main(string[] args)
        {
            string config = "config.json";
            string location = System.AppContext.BaseDirectory;

            Console.WriteLine("Settings up...");

            if (!Directory.Exists(Configurationfolder))
                Directory.CreateDirectory(Configurationfolder);

            if (!File.Exists(config))
            {
                // serialize JSON directly to a file
                using (StreamWriter file = File.CreateText(location + Path.DirectorySeparatorChar + "configuration" + Path.DirectorySeparatorChar + config))
                {
                    file.Write(JsonConvert.SerializeObject(new Config(), Formatting.Indented));
                }
            }

            _configuration = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Configurationfolder + Path.DirectorySeparatorChar + config));

            if (!Directory.Exists(Pluginsfolder))
                Directory.CreateDirectory(Pluginsfolder);

        }
    }
}