using System.Runtime.InteropServices;

namespace discord_rpc_test.discord
{
    public class DiscordRpc
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReadyCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DisconnectedCallback(int errorCode, string message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ErrorCallback(int errorCode, string message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void JoinCallback(string secret);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SpectateCallback(string secret);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RequestCallback(JoinRequest request);

        public struct EventHandlers
        {
            public ReadyCallback ReadyCallback;
            public DisconnectedCallback DisconnectedCallback;
            public ErrorCallback ErrorCallback;
            public JoinCallback JoinCallback;
            public SpectateCallback SpectateCallback;
            public RequestCallback RequestCallback;
        }

        [System.Serializable]
        public struct RichPresence
        {
            public string State; /* max 128 bytes */
            public string Details; /* max 128 bytes */
            public long StartTimestamp;
            public long EndTimestamp;
            public string LargeImageKey; /* max 32 bytes */
            public string LargeImageText; /* max 128 bytes */
            public string SmallImageKey; /* max 32 bytes */
            public string SmallImageText; /* max 128 bytes */
            public string PartyId; /* max 128 bytes */
            public int PartySize;
            public int PartyMax;
            public string MatchSecret; /* max 128 bytes */
            public string JoinSecret; /* max 128 bytes */
            public string SpectateSecret; /* max 128 bytes */
            public bool Instance;
        }

        [System.Serializable]
        public struct JoinRequest
        {
            public string UserId;
            public string Username;
            public string Avatar;
        }

        public enum Reply
        {
            No = 0,
            Yes = 1,
            Ignore = 2
        }

        [DllImport("discord-rpc", EntryPoint = "Discord_Initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId);

        [DllImport("discord-rpc", EntryPoint = "Discord_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Shutdown();

        [DllImport("discord-rpc", EntryPoint = "Discord_RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RunCallbacks();

        [DllImport("discord-rpc", EntryPoint = "Discord_UpdatePresence", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdatePresence(ref RichPresence presence);

        [DllImport("discord-rpc", EntryPoint = "Discord_Respond", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Respond(string userId, Reply reply);
    }
}

