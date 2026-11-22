using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("HometownStore.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.wightfall.hometownstoreaccess";
        public const string Name = "Hometown Store Access";
        public const string Version = "1.4.4";

        internal static Plugin Instance { get; private set; }
        internal static ModSettings Settings { get; private set; }
        internal static string PluginDirectory { get; private set; }
        internal static Harmony Harmony { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            PluginDirectory = Path.GetDirectoryName(Info.Location);
            Settings = new ModSettings(Config);
            Log.DebugEnabled = Settings.DebugLogging.Value;

            SpeechService.LogSpeech = Settings.LogSpeech.Value;
            SpeechService.Initialize(PluginDirectory, Settings.SpeechBackend.Value);

            try
            {
                Harmony = new Harmony(Guid);
                // One class at a time: a patch whose game method is gone (after a game update) is skipped and
                // said in the log, and every other patch still applies.
                foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
                {
                    try
                    {
                        Harmony.CreateClassProcessor(type).Patch();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Harmony patch " + type.FullName + " could not be applied: " + (e.InnerException ?? e).Message);
                    }
                }
                Log.Info("Harmony patches applied: " + string.Join(", ", Harmony.GetPatchedMethods().Select(m => m.DeclaringType?.Name + "." + m.Name).ToArray()));
            }
            catch (Exception e)
            {
                Log.Error("Applying Harmony patches failed: " + e);
            }

            var go = new GameObject("HometownStoreAccess");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<AccessibilityRoot>();
            Log.Info(Name + " " + Version + " initialised; speech backend " + SpeechService.BackendName);
        }
    }
}
