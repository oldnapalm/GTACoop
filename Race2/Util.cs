using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Race.Objects;

namespace Race
{
    class Util
    {
        public static string[] GetMaps()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "Gamemodes", "Maps");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return Directory.GetFiles(folder, "*.json");
        }

        public static string GetRandomMap()
        {
            var rand = new Random();
            return Race.Maps[rand.Next(Race.Maps.Count)].Name;
        }
    }
}
