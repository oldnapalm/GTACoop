using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// Split a string with or without quotes to arguments e.g. 'kick "wow a reason" Bob' becomes string[] { "kick", "wow a reason", "Bob" }
        /// </summary>
        /// <param name="command">The raw command</param>
        public static List<string> SplitCommandString(string command)
        {
            var i = 0;
            var inQuotes = false;
            var args = new List<string>();

            foreach (char rune in command.ToCharArray())
            {
                if (rune == ' ' && !inQuotes)
                {
                    i++;
                    continue;
                }

                if (rune == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (args.ElementAtOrDefault(i) == null) args.Add("");
                args[i] += rune;
            }

            return args;
        }

        /// <summary>
        /// Append **anonymous** telemetry to the master server request, this data is encrypted
        /// </summary>
        /// <param name="request"></param>
        internal static void AppendTelemetry(ref GameServer.MasterRequest request)
        {
            var rsaKeyInfo = new RSAParameters();
            rsaKeyInfo.Exponent = new byte[] { 0x01, 0x00, 0x01 };

            if(Modulus == null)
            {
                // get public key from assembly
                var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GTAServer.public.pem");
                Modulus = new byte[resource.Length];
                resource.Read(Modulus, 0, (int)resource.Length);
            }
            rsaKeyInfo.Modulus = Modulus;

            var rsa = RSA.Create();
            rsa.ImportParameters(rsaKeyInfo);

            // serialize and encrypt telemetry data
            var telemetry = JsonConvert.SerializeObject(GetTelemetryProperties());
            var payload = rsa.Encrypt(Encoding.UTF8.GetBytes(telemetry), RSAEncryptionPadding.Pkcs1);

            // append to request
            request.Telemetry = Convert.ToBase64String(payload);
        }

        internal static byte[] Modulus { get; private set; }

        /// <summary>
        /// Get all telemetry data
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, string> GetTelemetryProperties()
        {
            return new Dictionary<string, string>
            {
                // the version of the operating system
                { "OSVersion", RuntimeInformation.OSDescription },

                // the version of GTAServer.core
                { "Version", GetServerVersion() },
#if DEBUG
                { "Configuration", "Debug" }
#endif
#if RELEASE
                { "Configuration", "Release" }
#endif
            };
        }

        public static string GetServerVersion()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
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
            if (User != null)
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
