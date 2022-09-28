using Microsoft.Extensions.Logging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GTAServer.ProtocolMessages;
using System.Text.Json.Nodes;
using Cgmp.Shared.Preferences;

namespace GTAServer
{
    public static class Util
    {
        /// <summary>
        /// The server logger factory to produce logger instances
        /// </summary>
        public static ILoggerFactory LoggerFactory;

        /// <summary>
        /// Static instance of HttpClient that can be reused
        /// </summary>
        internal static HttpClient HttpClient;

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

        /// <summary>
        /// Removes any Rockstar text formatting from a string
        /// </summary>
        /// <param name="input">The string to sanitize</param>
        /// <returns>The sanitized string</returns>
        public static string SanitizeString(string input)
        {
            input = Regex.Replace(input, "~.~", "", RegexOptions.IgnoreCase);
            return input;
        }

        internal static void CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) => 
                {
                    // based of src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnectionPool.cs#L1338
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) 
                    {
                        NoDelay = true 
                    };

                    socket.Bind(new IPEndPoint(IPAddress.Any, 0)); // IPv4 only please

                    try
                    {
                        await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, true);
                    }
                    catch(Exception)
                    {
                        socket.Dispose();

                        throw;
                    }
                }
            };

            HttpClient = new HttpClient(handler);

            HttpClient.DefaultRequestHeaders
                .TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (GTAServer.core " + GetServerVersion() + ")");
        }

        /// <summary>
        /// Split a string with or without quotes to arguments e.g. 'kick "wow a reason" Bob' becomes string[] { "kick", "wow a reason", "Bob" }
        /// </summary>
        /// <param name="command">The raw command</param>
        public static List<string> SplitCommandString(string command)
        {
            var i = 0;
            var inQuotes = false;
            var previousCharacter = (char)0;
            var args = new List<string>();

            foreach (char rune in command.ToCharArray())
            {
                if (rune == ' ' && !inQuotes)
                {
                    if (previousCharacter != ' ') i++;
                    previousCharacter = rune;
                    continue;
                }

                if (rune == '"')
                {
                    inQuotes = !inQuotes;
                    previousCharacter = rune;
                    continue;
                }

                previousCharacter = rune;

                if (args.ElementAtOrDefault(i) == null) args.Add("");
                args[i] += rune;
            }

            return args;
        }

        #region telemetry

        /// <summary>
        /// Append encrypted telemetry to the master server request
        /// </summary>
        /// <param name="request">The master server request to append to</param>
        internal static void AppendTelemetry(ref GameServer.MasterRequest request)
        {
            // ensure public key
            if (PublicKey == null)
            {
                PublicKey = GetTelemetryPublic();
            }

            // create RSA
            var rsaKeyInfo = new RSAParameters();
            rsaKeyInfo.Modulus = PublicKey.Item1;
            rsaKeyInfo.Exponent = PublicKey.Item2;

            var rsa = RSA.Create();
            rsa.ImportParameters(rsaKeyInfo);

            // serialize and encrypt telemetry data
            var telemetry = JsonSerializer.Serialize(GetTelemetryProperties());
            var payload = rsa.Encrypt(Encoding.UTF8.GetBytes(telemetry), RSAEncryptionPadding.Pkcs1);

            // append to request
            request.Telemetry = Convert.ToBase64String(payload);
            rsa.Dispose();
        }

        private static Tuple<byte[], byte[]> PublicKey { get; set; }

        /// <summary>
        /// Get all telemetry data
        /// </summary>
        /// <returns>The telemetry data</returns>
        private static Dictionary<string, string> GetTelemetryProperties()
        {
            return new Dictionary<string, string>
            {
                // randomly generated id for this install
                { "ServerID", InstallationInfo.Instance.ServerId },

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

        /// <summary>
        /// Gets the telemetry public key embedded in the server binary
        /// </summary>
        /// <returns>The public key modulus and exponent</returns>
        private static Tuple<byte[], byte[]> GetTelemetryPublic()
        {
            // loosely based on json web keys
            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GTAServer.public.json");
            var key = JsonSerializer.Deserialize<JsonNode>(resource);

            if ((string)key["kty"] != "RSA")
            {
                throw new Exception("Invalid telemetry key");
            }

            var modulus = Convert.FromBase64String((string)key["n"]);
            var exponent = Convert.FromBase64String((string)key["e"]);

            return new Tuple<byte[], byte[]>(modulus, exponent);
        }

        #endregion

        /// <summary>
        /// Gets the current server version
        /// </summary>
        /// <returns>The version of the server</returns>
        public static string GetServerVersion()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        /// <summary>
        /// Converts a <see cref="ScriptVersion"/> to human readable
        /// </summary>
        /// <param name="version">The version to convert</param>
        /// <returns>Human readable form of the version</returns>
        public static string ToReadable(this ScriptVersion version)
        {
            var readable = version.ToString();
            readable = Regex.Replace(readable, "VERSION_", "", RegexOptions.IgnoreCase);
            return Regex.Replace(readable, "_", ".", RegexOptions.IgnoreCase);
        }
    }

    // information about the current server installation, backed by a cross preference file
    internal class InstallationInfo : CrossPreferenceBase
    {
        // instance of installation info
        public static InstallationInfo Instance = (InstallationInfo)Synchronize("Data/Installation.cpf", new InstallationInfo());

        // set the defaults
        public override void Defaults()
        {
            this["server_id"] = Guid.NewGuid().ToString();
            this["install_time"] = DateTimeOffset.Now.ToString();
        }

        public string ServerId
        {
            get
            {
                return this["server_id"];
            }
        }

        public string Installed
        {
            get
            {
                return this["install_time"];
            }
        }
    }
}
