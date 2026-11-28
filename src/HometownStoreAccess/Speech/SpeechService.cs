using System;
using System.IO;
using System.Threading;
using HometownStoreAccess.Core;

namespace HometownStoreAccess.Speech
{
    /// <summary>
    /// Sends text to the user's screen reader (NVDA, JAWS, ZDSR, ...) or a TTS engine through
    /// Prism. Text queued during a frame is combined and spoken once at the end of the frame.
    /// When a screen reader starts or stops while the game runs, Prism's availability poll tells
    /// us and the best backend is selected again on the main thread.
    /// </summary>
    internal static class SpeechService
    {
        private static IntPtr _ctx = IntPtr.Zero;
        private static IntPtr _backend = IntPtr.Zero;
        private static PrismNative.AvailabilityCallback _callbackKeepAlive;
        private static int _reselectRequested;
        // Set when speaking failed: the backend is made anew even if it is the same one (its old instance is broken).
        private static int _replaceRequested;
        private static int _lastReplaceTick;
        private static bool _initialized;
        private static string _preferredBackend = "Auto";

        public static readonly SpeechBuffer Buffer = new SpeechBuffer(historyLimit: 200);

        public static string BackendName { get; private set; } = "none";
        public static bool Available => _backend != IntPtr.Zero;
        public static bool LogSpeech { get; set; } = true;
        public static bool Muted { get; set; }

        /// <summary>Raised (main thread) for every utterance actually sent; used by tests and the debug log.</summary>
        public static event Action<string, bool> Spoken;

        public static void Initialize(string pluginDirectory, string preferredBackend)
        {
            if (_initialized) return;
            _initialized = true;
            _preferredBackend = string.IsNullOrEmpty(preferredBackend) ? "Auto" : preferredBackend.Trim();
            try
            {
                string dllPath = Path.Combine(pluginDirectory, "prism.dll");
                if (File.Exists(dllPath))
                {
                    IntPtr module = PrismNative.LoadLibraryW(dllPath);
                    if (module == IntPtr.Zero)
                    {
                        Log.Error("Could not load prism.dll from " + dllPath + " (Win32 error " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ")");
                    }
                }
                else
                {
                    Log.Warn("prism.dll not found next to the plugin at " + dllPath + "; relying on the default search path.");
                }

                Log.Info("Prism version " + PrismNative.FromUtf8(PrismNative.prism_version_string()));
                _ctx = CreateContext();
                if (_ctx == IntPtr.Zero)
                {
                    Log.Error("prism_init failed; speech is unavailable.");
                    return;
                }
                SelectBackend(announce: false);
            }
            catch (Exception e)
            {
                Log.Error("Speech initialisation failed: " + e);
            }
        }

        private static IntPtr CreateContext()
        {
            try
            {
                PrismNative.PrismConfig cfg = PrismNative.prism_config_init();
                if (cfg.version != PrismNative.PrismConfigVersion)
                {
                    Log.Warn("Unexpected Prism config version " + cfg.version + "; starting without availability monitoring.");
                    return PrismNative.prism_init(IntPtr.Zero);
                }
                _callbackKeepAlive = OnAvailabilityChanged;
                cfg.availability_callback = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_callbackKeepAlive);
                IntPtr ctx = PrismNative.prism_init(ref cfg);
                if (ctx != IntPtr.Zero) return ctx;
                Log.Warn("prism_init with availability monitoring failed; retrying with defaults.");
            }
            catch (Exception e)
            {
                Log.Warn("Prism availability monitoring unavailable: " + e.Message);
            }
            return PrismNative.prism_init(IntPtr.Zero);
        }

        // Called on Prism's poll thread. Only records that a reselection is needed.
        private static void OnAvailabilityChanged(IntPtr userdata, ulong backend, IntPtr name, bool available)
        {
            Interlocked.Exchange(ref _reselectRequested, 1);
        }

        /// <summary>Picks the configured backend, or the best available one ("Auto").</summary>
        public static void SelectBackend(bool announce) => SelectBackend(announce, false);

        /// <param name="replace">Use the newly made backend even when it is the same kind as the current one.</param>
        private static void SelectBackend(bool announce, bool replace)
        {
            if (_ctx == IntPtr.Zero) return;
            IntPtr chosen = IntPtr.Zero;
            bool preferredFailed = false;
            try
            {
                if (!string.Equals(_preferredBackend, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = CreateNamedBackend(_preferredBackend);
                    if (chosen == IntPtr.Zero)
                    {
                        preferredFailed = true;
                        Log.Warn("Preferred speech backend '" + _preferredBackend + "' is not available; using the best available backend.");
                    }
                }
                if (chosen == IntPtr.Zero) chosen = PrismNative.prism_registry_create_best(_ctx);
            }
            catch (Exception e)
            {
                Log.Error("Selecting a speech backend failed: " + e.Message);
            }

            if (chosen == IntPtr.Zero)
            {
                Log.Error("No speech backend is available.");
                return;
            }

            string newName = PrismNative.FromUtf8(PrismNative.prism_backend_name(chosen));
            if (_backend != IntPtr.Zero)
            {
                bool same = string.Equals(newName, BackendName, StringComparison.Ordinal);
                if (same && !replace)
                {
                    PrismNative.prism_backend_free(chosen);
                    if (announce && preferredFailed) Say(Loc.Get("speech_backend_unavailable", _preferredBackend, BackendName), true);
                    return;
                }
                PrismNative.prism_backend_free(_backend);
                // The same output started again: nothing to tell the player.
                if (same) announce = false;
            }
            _backend = chosen;
            BackendName = newName;
            Log.Info("Speech backend: " + BackendName);
            if (announce)
            {
                Say(preferredFailed ? Loc.Get("speech_backend_unavailable", _preferredBackend, BackendName) : Loc.Get("speech_backend_changed", BackendName), true);
            }
        }

        private static IntPtr CreateNamedBackend(string name)
        {
            int count = (int)PrismNative.prism_registry_count(_ctx);
            for (int i = 0; i < count; i++)
            {
                ulong id = PrismNative.prism_registry_id_at(_ctx, (UIntPtr)i);
                string n = PrismNative.FromUtf8(PrismNative.prism_registry_name(_ctx, id));
                if (!string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) continue;
                IntPtr backend = PrismNative.prism_registry_create(_ctx, id);
                if (backend == IntPtr.Zero) return IntPtr.Zero;
                // Created by name, the backend has to be started before it can speak.
                int err = PrismNative.prism_backend_initialize(backend);
                if (err == PrismNative.PrismOk || err == PrismNative.PrismErrorAlreadyInitialized) return backend;
                Log.Warn("Speech backend '" + n + "' could not start: " + PrismNative.FromUtf8(PrismNative.prism_error_string(err)) + ".");
                PrismNative.prism_backend_free(backend);
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        /// <summary>Switches the preferred backend ("Auto" or a name) and reselects immediately.</summary>
        public static void SetPreferred(string name)
        {
            _preferredBackend = string.IsNullOrEmpty(name) ? "Auto" : name.Trim();
            SelectBackend(announce: true);
        }

        // Prism 0.18.4 backends that are not speech for a player: one copies every message over the
        // clipboard, the other says nothing. They stay out of the settings list (PRISM_BACKEND_CLIPBOARD,
        // PRISM_BACKEND_NO_SPEECH in prism.h).
        private const ulong ClipboardBackendId = 0x7056908DE8F9F991UL;
        private const ulong NoSpeechBackendId = 0xC2C0EAB0BF97A5F0UL;

        /// <summary>Names of the backends Prism knows on this system (available or not) that a player can choose.</summary>
        public static System.Collections.Generic.List<string> BackendNames()
        {
            var list = new System.Collections.Generic.List<string>();
            if (_ctx == IntPtr.Zero) return list;
            int count = (int)PrismNative.prism_registry_count(_ctx);
            for (int i = 0; i < count; i++)
            {
                ulong id = PrismNative.prism_registry_id_at(_ctx, (UIntPtr)i);
                if (id == ClipboardBackendId || id == NoSpeechBackendId) continue;
                list.Add(PrismNative.FromUtf8(PrismNative.prism_registry_name(_ctx, id)));
            }
            return list;
        }

        /// <summary>Queues text to be spoken at the end of this frame.</summary>
        public static void Say(string text, bool interrupt = true, MsgCategory category = MsgCategory.All)
        {
            if (string.IsNullOrEmpty(text)) return;
            Buffer.Add(text, interrupt);
            string clean = TextCleaner.Clean(text);
            if (clean.Length > 0 && clean != _lastLogged && (HistoryEnabled == null || HistoryEnabled()))
            {
                _lastLogged = clean;
                History.Add(clean, category);
            }
        }

        /// <summary>Categorised history of everything spoken (reviewed with , . [ ]).</summary>
        public static readonly MessageLog History = new MessageLog();

        /// <summary>Whether spoken messages are kept in the history now: only while playing.</summary>
        public static Func<bool> HistoryEnabled;

        /// <summary>The latest conversation, for Backslash and Shift+Backslash inside it.</summary>
        public static readonly StoryRecall Recall = new StoryRecall();
        private static string _lastLogged;

        /// <summary>Speaks immediately (bypassing the frame buffer). Use sparingly.</summary>
        public static void SayNow(string text, bool interrupt = true)
        {
            Buffer.Add(text, interrupt);
            Flush(UnityEngine.Time.realtimeSinceStartup);
        }

        /// <summary>Speaks text without recording it in the history (used while reviewing the history).</summary>
        public static void SpeakReview(string text)
        {
            Buffer.ClearPending();
            Send(TextCleaner.Clean(text), true);
        }

        public static void Stop()
        {
            Buffer.ClearPending();
            if (_backend == IntPtr.Zero) return;
            try { PrismNative.prism_backend_stop(_backend); } catch { }
        }

        /// <summary>Main-thread pump: handles backend reselection and flushes the frame buffer.</summary>
        public static void Update(double now)
        {
            bool replace = Interlocked.Exchange(ref _replaceRequested, 0) == 1;
            if (Interlocked.Exchange(ref _reselectRequested, 0) == 1 || replace)
            {
                SelectBackend(true, replace);
            }
            Flush(now);
        }

        private static void Flush(double now)
        {
            Utterance? u = Buffer.Flush(now);
            if (!u.HasValue) return;
            Send(u.Value.Text, u.Value.Interrupt);
        }

        private static void Send(string text, bool interrupt)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (LogSpeech) Log.Info("[Speech" + (interrupt ? "!" : "") + "] " + text);
            Diagnostics.TestTrace.Speech(text);
            try { Spoken?.Invoke(text, interrupt); } catch (Exception e) { Log.Warn("Spoken handler failed: " + e.Message); }
            if (Muted || _backend == IntPtr.Zero) return;
            try
            {
                int err = PrismNative.prism_backend_output(_backend, PrismNative.Utf8Z(text), interrupt);
                if (err != 0)
                {
                    // At most once a second, so a backend that keeps failing is not made anew every frame.
                    int tick = Environment.TickCount;
                    if (unchecked(tick - _lastReplaceTick) > 1000 || _lastReplaceTick == 0)
                    {
                        _lastReplaceTick = tick;
                        Log.Warn("Prism output failed: " + PrismNative.FromUtf8(PrismNative.prism_error_string(err)) + "; starting the speech backend again.");
                        Interlocked.Exchange(ref _replaceRequested, 1);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Prism output threw: " + e.Message);
            }
        }

        public static void Shutdown()
        {
            try
            {
                if (_backend != IntPtr.Zero) PrismNative.prism_backend_free(_backend);
                _backend = IntPtr.Zero;
                if (_ctx != IntPtr.Zero) PrismNative.prism_shutdown(_ctx);
                _ctx = IntPtr.Zero;
            }
            catch (Exception e)
            {
                Log.Warn("Speech shutdown: " + e.Message);
            }
        }
    }
}
