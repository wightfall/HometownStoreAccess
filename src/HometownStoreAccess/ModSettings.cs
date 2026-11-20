using BepInEx.Configuration;

namespace HometownStoreAccess
{
    /// <summary>User-editable settings (BepInEx/config/com.wightfall.hometownstoreaccess.cfg).</summary>
    internal sealed class ModSettings
    {
        public readonly ConfigEntry<string> SpeechBackend;
        public readonly ConfigEntry<bool> LogSpeech;
        public readonly ConfigEntry<string> ModLanguage;

        public readonly ConfigEntry<bool> AnnounceCustomers;
        public readonly ConfigEntry<bool> AnnounceSales;
        public readonly ConfigEntry<bool> AnnounceStock;
        public readonly ConfigEntry<bool> AnnounceCustomerMessages;
        public readonly ConfigEntry<bool> AnnounceCashChanges;
        public readonly ConfigEntry<bool> ReadDialogAutomatically;

        public readonly ConfigEntry<bool> AnnouncePosition;
        public readonly ConfigEntry<string> GameLocale;

        public readonly ConfigEntry<bool> AudioCues;
        public readonly ConfigEntry<bool> MenuSounds;
        public readonly ConfigEntry<float> AudioCueVolume;
        public readonly ConfigEntry<bool> CameraFollowsCursor;
        public readonly ConfigEntry<bool> ControllerEnabled;
        public readonly ConfigEntry<bool> ControllerRumble;

        public readonly ConfigEntry<bool> AnnounceTutorial;
        private readonly ConfigFile _config;
        private readonly System.Collections.Generic.Dictionary<Audio.Cue, ConfigEntry<float>> _cueVolumes = new System.Collections.Generic.Dictionary<Audio.Cue, ConfigEntry<float>>();

        public readonly ConfigEntry<bool> DebugLogging;
        public readonly ConfigEntry<bool> TestHooks;
        public readonly ConfigEntry<bool> AllowDevCheats;

        public ModSettings(ConfigFile config)
        {
            _config = config;
            SpeechBackend = config.Bind("Speech", "Backend", "Auto",
                "Speech output used through Prism. 'Auto' picks your running screen reader (NVDA, JAWS, ZDSR, ...) or falls back to Windows speech. You can force one by name, e.g. NVDA, JAWS, OneCore, SAPI.");
            LogSpeech = config.Bind("Speech", "LogSpeech", true, "Also write everything that is spoken to the BepInEx log (useful for bug reports).");
            ModLanguage = config.Bind("Speech", "ModLanguage", "Auto",
                "Language of the mod's own messages: Auto (follow the game), en, th, ja, id, zh, or the code of a translation file in the lang folder (es for es.mod.txt).");

            AnnouncePosition = config.Bind("Speech", "AnnouncePosition", true, "Say the position in lists, e.g. '3 of 8'.");
            GameLocale = config.Bind("Speech", "GameLocale", "",
                "Game language chosen through the mod (any of the game's languages, e.g. ja, id, zh-HK). Empty = use the game's own setting.");

            AnnounceCustomers = config.Bind("Announcements", "Customers", true, "Announce customers arriving, finding what they want, getting angry and leaving.");
            AnnounceSales = config.Bind("Announcements", "Sales", true, "Announce money received when a customer pays at the cashier.");
            AnnounceStock = config.Bind("Announcements", "Stock", true, "Announce when a shelf runs empty while the shop is open.");
            AnnounceCustomerMessages = config.Bind("Announcements", "CustomerMessages", true, "Read customer speech bubbles from the message box.");
            AnnounceCashChanges = config.Bind("Announcements", "CashChanges", false, "Announce every change of cash (can be chatty).");
            ReadDialogAutomatically = config.Bind("Announcements", "ReadDialog", true, "Read story dialog lines automatically as they appear.");

            AudioCues = config.Bind("Audio", "Cues", true, "Play positional audio cues for the shop floor cursor, placement, customers and menus.");
            MenuSounds = config.Bind("Audio", "MenuSounds", true, "Play short sounds when moving through menus, wrapping around and reaching the edge of a list.");
            AudioCueVolume = config.Bind("Audio", "CueVolume", 0.6f, new ConfigDescription("Volume of the mod's audio cues.", new AcceptableValueRange<float>(0f, 1f)));
            CameraFollowsCursor = config.Bind("Controls", "CameraFollowsCursor", true, "Keep the camera centred on the keyboard shop-floor cursor (arrow keys then move the cursor instead of the camera).");
            ControllerEnabled = config.Bind("Controls", "Controller", true, "Enable Xbox-compatible (XInput) controller support. Other controllers work through Steam Input.");
            ControllerRumble = config.Bind("Controls", "ControllerRumble", true, "Short controller vibration for errors and blocked placement.");

            AnnounceTutorial = config.Bind("Announcements", "Tutorial", true, "Read tutorial notes and the mod's key hints for each tutorial step.");
            foreach (Audio.Cue cue in System.Enum.GetValues(typeof(Audio.Cue)))
            {
                _cueVolumes[cue] = config.Bind("CueVolumes", cue.ToString(), 1f, new ConfigDescription("Volume of the " + cue + " sound cue, relative to CueVolume.", new AcceptableValueRange<float>(0f, 1f)));
            }

            DebugLogging = config.Bind("Debug", "Verbose", false, "Write detailed diagnostic messages to the log.");
            TestHooks = config.Bind("Debug", "TestHooks", false, "Developer only: read commands from BepInEx/hsa_cmd.txt (UI dumps, simulated keys).");
            AllowDevCheats = config.Bind("Debug", "AllowDevCheats", false, "Keep the game's hidden developer cheats when they were switched on (pressing F1 three times quickly). Off: the mod switches them off at start, since F1 is its help key.");
        }

        public ConfigEntry<float> CueVolume(Audio.Cue cue) => _cueVolumes[cue];

        /// <summary>Puts every option (except the developer ones) back to its default value.</summary>
        public void ResetToDefaults()
        {
            foreach (ConfigDefinition def in _config.Keys)
            {
                if (def.Section == "Debug") continue;
                ConfigEntryBase entry = _config[def];
                entry.BoxedValue = entry.DefaultValue;
            }
            _config.Save();
        }
    }
}
