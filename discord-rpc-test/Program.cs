using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using discord_rpc_test.discord;
using Lidgren.Network;

namespace discord_rpc_test
{
    class Program
    {

        static void Main(string[] args)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var config = new NetPeerConfiguration("GTAVOnlineRaces");
            //config.Port = new Random().Next(1000, 9999);

            var client = new NetClient(config);
            client.Start();

            var msg = client.CreateMessage("query");

            client.SendUnconnectedMessage(msg, new IPEndPoint(NetUtility.Resolve("127.0.0.1"), 4499));

            client.MessageReceivedEvent.WaitOne();

            var response = client.ReadMessage();
            Console.WriteLine(response);

            Console.ReadLine();
        }

        /*private static WaveInEvent _captureDevice;

        static void Main(string[] args)
        {
            // shitty code to test shit (fix in client)
            var deviceEnum = new MMDeviceEnumerator();
            var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            Console.WriteLine("Found following devices: \n" + String.Join("\n ", devices.Select(n => "- " + n.FriendlyName)));

            _captureDevice = new WaveInEvent()
            {
                DeviceNumber = 0
            };

            var device = devices.FirstOrDefault();
            device.AudioEndpointVolume.Mute = false;

            Console.WriteLine("Using " + devices[_captureDevice.DeviceNumber].FriendlyName);

            _captureDevice.StartRecording();

            var provider = new BufferedWaveProvider(new WaveFormat());
            var writer = new WaveFileWriter("tes.wav", new WaveFormat());

            var player = new WaveOut();
            _captureDevice.WaveFormat = WaveFormat.CreateALawFormat(0, 100);
            player.Init(provider);

            _captureDevice.DataAvailable += (sender, argss) =>
            {
                Console.WriteLine("Hehe data avaible: " + argss.BytesRecorded);

                writer.Write(argss.Buffer, 0, argss.BytesRecorded);
            };

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                writer.Close();
            };

            //stay alive :)
            while (true)
            {

            }
        }*/
        }
}
