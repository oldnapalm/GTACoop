using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;

namespace GTAServer
{
    public class Util
    {
        public static T DeserializeBinary<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public static ILoggerFactory LoggerFactory;

        public static string SanitizeString(string input)
        {
            input = Regex.Replace(input, "~.~", "", RegexOptions.IgnoreCase);
            return input;
        }   
    }

    public class Discord
    {
        private static DiscordUser User { get; set; }

        /// <summary>
        /// Returns the current discord user of the local discord client
        /// </summary>
        /// <returns>The discord user object</returns>
        public static DiscordUser GetDiscordUser()
        {
            if(User != null)
            {
                return User;
            }

            FileStream pipe = null;

            try
            {
                // open discord named pipe
                pipe = File.Open("\\\\?\\pipe\\discord-ipc-0", FileMode.Open, FileAccess.ReadWrite);

                var handshake = JObject.FromObject(new
                {
                    v = 1,
                    client_id = "348838311873216514"
                });

                var json = Encoding.UTF8.GetBytes(handshake.ToString());

                // write handshake to pipe
                var payload = new BinaryWriter(new MemoryStream());
                payload.Write(0);
                payload.Write(json.Length);
                payload.Write(json);

                var array = (payload.BaseStream as MemoryStream).ToArray();

                pipe.Write(array, 0, array.Length);

                // discord will respond with READY event containing user object
                var response = new byte[1024];
                pipe.Read(response, 0, response.Length);

                User = JObject.Parse(Encoding.UTF8.GetString(response.Skip(8).ToArray()))
                    .Value<JObject>("data")
                    .Value<JObject>("user")
                    .ToObject<DiscordUser>();

                return User;
            }
            catch (Exception)
            {
                // we don't care if anything above fails
                return null;
            }
            finally
            {
                pipe?.Close();
            }
        }

        public class DiscordUser
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("username")]
            public string Name { get; set; }

            [JsonProperty("discriminator")]
            public string Discriminator { get; set; }
        }
    }
}
