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

namespace GTAServer
{
    public static class Util
    {
        public static ILoggerFactory LoggerFactory;
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
            var telemetry = JsonSerializer.Serialize(GetTelemetryProperties());
            var payload = rsa.Encrypt(Encoding.UTF8.GetBytes(telemetry), RSAEncryptionPadding.Pkcs1);

            // append to request
            request.Telemetry = Convert.ToBase64String(payload);
            rsa.Dispose();
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

        public static string ToReadable(this ScriptVersion version)
        {
            var readable = version.ToString();
            readable = Regex.Replace(readable, "VERSION_", "", RegexOptions.IgnoreCase);
            return Regex.Replace(readable, "_", ".", RegexOptions.IgnoreCase);
        }
    }
}
