using BepInEx.Logging;

namespace HometownStoreAccess
{
    internal static class Log
    {
        private static ManualLogSource _source;

        public static void Init(ManualLogSource source) => _source = source;

        public static bool DebugEnabled { get; set; }

        public static void Info(string msg) => _source?.LogInfo(msg);
        public static void Warn(string msg) => _source?.LogWarning(msg);
        public static void Error(string msg) => _source?.LogError(msg);

        public static void Debug(string msg)
        {
            if (DebugEnabled) _source?.LogInfo("[dbg] " + msg);
        }
    }
}
