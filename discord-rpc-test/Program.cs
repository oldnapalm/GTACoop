using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using discord_rpc_test.discord;
using Microsoft.VisualBasic;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave;

namespace discord_rpc_test
{
    class Program
    {
        private static WaveInEvent _captureDevice;

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

            var writer = new WaveFileWriter("testt.wav", _captureDevice.WaveFormat);

            _captureDevice.DataAvailable += (sender, argss) =>
            {
                Console.WriteLine("Hehe data avaible: " + argss.BytesRecorded);

                writer.Write(argss.Buffer, Convert.ToInt32(writer.Position), argss.BytesRecorded);
            };

            Console.CancelKeyPress += (arg, arg2) =>
            {
                writer.Close();
            };

            //stay alive :)
            while (true)
            {
               
            }
        }
    }
}
