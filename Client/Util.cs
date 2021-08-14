using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using GTA;
using GTA.Math;
using GTA.Native;
//using GTAServer;
using static System.Runtime.InteropServices.Marshal;
namespace GTACoOp
{
    public static class Util
    {
        public static void DisplayHelpText(string text)
        {
            Function.Call(Hash._SET_TEXT_COMPONENT_FORMAT, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0x238FFE5C7B0498A6, 0, 0, 1, -1);
        }

        public static int GetStationId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

        public static string GetStationName(int id)
        {
            return Function.Call<string>(Hash.GET_RADIO_STATION_NAME, id);
        }

        public static string[] GetRadioStations()
        {
            return (string[]) typeof(Game).GetProperty("radioNames")?.GetValue(typeof(Game));
        }

        public static int GetTrackId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
        }

        public static bool IsVehicleEmpty(Vehicle veh)
        {
            if (veh == null) return true;
            if (!veh.IsSeatFree(VehicleSeat.Driver)) return false;
            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (!veh.IsSeatFree((VehicleSeat)i))
                    return false;
            }
            return true;
        }

        public static Dictionary<int, int> GetVehicleMods(Vehicle veh)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                dict.Add(i, veh.GetMod((VehicleMod)i));
            }
            return dict;
        }

        public static Dictionary<int, int> GetPlayerProps(Ped ped)
        {
            var props = new Dictionary<int, int>();
            for (int i = 0; i < 15; i++)
            {
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, i);
                if (mod == -1) continue;
                props.Add(i, mod);
            }
            return props;
        }

        public static int GetPedSeat(Ped ped)
        {
            if (ped == null || !ped.IsInVehicle()) return -3;
            if (ped.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < ped.CurrentVehicle.PassengerSeats; i++)
            {
                if (ped.CurrentVehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static int GetFreePassengerSeat(Vehicle veh)
        {
            if (veh == null) return -3;
            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (veh.IsSeatFree((VehicleSeat)i))
                    return i;
            }
            return -3;
        }

        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new PlayerSettings());
            }

            return settings;
        }

        public static void SaveSettings(string path)
        {
            try {
                if (string.IsNullOrEmpty(path)) { path = Program.Location + Path.DirectorySeparatorChar + "ClientSettings.xml"; }
                var ser = new XmlSerializer(typeof(PlayerSettings));
                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, Main.PlayerSettings);
                if (Main.PlayerSettings.Logging)
                {
                    File.AppendAllText("scripts\\GTACOOP.log", "Saved settings to " + path);
                }
            } catch (Exception ex) {
                UI.Notify("Error saving player settings: " + ex.Message);
            }
        }

        public static Vector3 GetLastWeaponImpact(Ped ped)
        {
            var coord = new OutputArgument();
            if (!Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, ped.Handle, coord))
            {
                return new Vector3();
            }
            return coord.GetResult<Vector3>();
        }

        public static Quaternion LerpQuaternion(Quaternion start, Quaternion end, float speed)
        {
            return new Quaternion()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
                W = start.W + (end.W - start.W) * speed,
            };
        }

        public static Vector3 LerpVector(Vector3 start, Vector3 end, float speed)
        {
            return new Vector3()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
            };
        }

        public static Vector3 QuaternionToEuler(Quaternion quat)
        {
            //heading = atan2(2*qy*qw-2*qx*qz , 1 - 2*qy2 - 2*qz2) (yaw)
            //attitude = asin(2 * qx * qy + 2 * qz * qw) (pitch)
            //bank = atan2(2 * qx * qw - 2 * qy * qz, 1 - 2 * qx2 - 2 * qz2) (roll)

            return new Vector3()
            {
                X = (float)Math.Asin(2 * quat.X * quat.Y + 2 *quat.Z * quat.W),
                Y = (float)Math.Atan2(2 * quat.X * quat.W - 2 * quat.Y * quat.Z, 1 -  2 * quat.X*quat.X - 2 * quat.Z * quat.Z),
                Z = (float)Math.Atan2(2*quat.Y*quat.W - 2*quat.X*quat.Z, 1 - 2*quat.Y*quat.Y - 2*quat.Z * quat.Z),
            };

            /*except when qx*qy + qz*qw = 0.5 (north pole)
            which gives:
            heading = 2 * atan2(x,w)
            bank = 0

            and when qx*qy + qz*qw = -0.5 (south pole)
            which gives:
            heading = -2 * atan2(x,w)
            bank = 0 */
        }

        public static Vector3 LinearVectorLerp(Vector3 start, Vector3 end, int currentTime, int duration)
        {
            return new Vector3()
            {
                X = LinearFloatLerp(start.X, end.X, currentTime, duration),
                Y = LinearFloatLerp(start.Y, end.Y, currentTime, duration),
                Z = LinearFloatLerp(start.Z, end.Z, currentTime, duration),
            };
        }

        public static float LinearFloatLerp(float start, float end, int currentTime, int duration)
        {
            float change = end - start;
            return change * currentTime / duration + start;
        }

        public static void ShowBusySpinner(string text)
        {
            Function.Call((Hash)0xABA17D7CE615ADBF /* BEGIN_TEXT_COMMAND_BUSYSPINNER_ON */, "STRING");
            Function.Call((Hash)0x6C188BE134E074AA /* ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME */, text);
            Function.Call((Hash)0xBD12F8228410D9B4 /* END_TEXT_COMMAND_BUSYSPINNER_ON */, 0);
        }

        public static void HideBusySpinner()
        {
            Function.Call((Hash)0x10D373323E5B9C0D /* BUSYSPINNER_OFF */);
        }

        private static int SteeringAngleOffset { get; set; }

        static unsafe byte* FindPattern(string pattern, string mask)
        {
            ProcessModule module = Process.GetCurrentProcess().MainModule;
            return FindPattern(pattern, mask, module.BaseAddress, (ulong)module.ModuleMemorySize);
        }

        static unsafe byte* FindPattern(string pattern, string mask, IntPtr startAddress, ulong size)
        {
            ulong address = (ulong)startAddress.ToInt64();
            ulong endAddress = address + size;

            for (; address < endAddress; address++)
            {
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (mask[i] != '?' && ((byte*)address)[i] != pattern[i])
                    {
                        break;
                    }
                    else if (i + 1 == pattern.Length)
                    {
                        return (byte*)address;
                    }
                }
            }

            return null;
        }

        public static unsafe void NativeMemory()
        {
            byte* address;

            address = FindPattern("\xE8\x00\x00\x00\x00\x48\x8B\xD8\x48\x85\xC0\x74\x2E\x48\x83\x3D", "x????xxxxxxxxxxx");
            GetEntityAddressFunc = GetDelegateForFunctionPointer<GetHandleAddressFuncDelegate>(new IntPtr(*(int*)(address + 1) + address + 5));

            // use the former pattern if the version is 1.0.1604.0 or newer
            int gameVersion = (int)Game.Version;
            address = gameVersion >= 46 ?
                        FindPattern("\xF3\x0F\x10\x9F\xD4\x08\x00\x00\x0F\x2F\xDF\x73\x0A", "xxxx????xxxxx") :
                        FindPattern("\xF3\x0F\x10\x8F\x68\x08\x00\x00\x88\x4D\x8C\x0F\x2F\xCF", "xxxx????xxx???");

            address = FindPattern("\x74\x0A\xF3\x0F\x11\xB3\x1C\x09\x00\x00\xEB\x25", "xxxxxx????xx");
            if (address != null)
            {
                SteeringAngleOffset = *(int*)(address + 6) + 8;
            }

            address = FindPattern("\x32\xc0\xf3\x0f\x11\x09", "xxxxxx"); // Weapon / Radio slowdown
            if (address != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    *(byte*)((IntPtr)address + i).ToPointer() = 0x90;
                }
            }
        }

        delegate ulong GetHandleAddressFuncDelegate(int handle);
        static GetHandleAddressFuncDelegate GetEntityAddressFunc;

        public static IntPtr GetEntityAddress(int handle)
        {
            return new IntPtr((long)GetEntityAddressFunc(handle));
        }

        public static unsafe void CustomSteeringAngle(int Handle, float value)
        {
            IntPtr address = GetEntityAddress(Handle);
            if (address == IntPtr.Zero || SteeringAngleOffset == 0)
            {
                return;
            }

            *(float*)(address + SteeringAngleOffset).ToPointer() = value;
        }
    }
}