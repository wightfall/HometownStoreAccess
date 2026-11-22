using System;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Contexts;
using HometownStoreAccess.Core;
using HometownStoreAccess.Diagnostics;
using HometownStoreAccess.Game;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HometownStoreAccess
{
    /// <summary>The mod's persistent MonoBehaviour: drives input, contexts, announcements and speech.</summary>
    internal sealed class AccessibilityRoot : MonoBehaviour
    {
        public static AccessibilityRoot Instance { get; private set; }

        public readonly ContextManager Contexts = new ContextManager();

        private bool _welcomed;
        private string _sceneName = string.Empty;
        private float _sceneConfiguredAt;

        public string SceneName => _sceneName;

        /// <summary>When the current scene finished loading (unscaled time).</summary>
        public float SceneLoadedAt { get; private set; } = -100f;
        public bool InGame => _sceneName == SceneSetup.GameScene;

        /// <summary>
        /// Playing the game: in the game scene, not in the pause menu or the save screen, and none of
        /// the mod's own screens (key list, settings) open. Only then are messages kept in the history,
        /// and only then do the history keys work.
        /// </summary>
        public bool GameplayActive
        {
            get
            {
                if (!InGame || Overlay.IsOpen) return false;
                try
                {
                    GlobeOption o = GlobeOption.Instance;
                    if (o != null && o.optionPanel != null && o.GetOptionPanelActiveSelf()) return false;
                }
                catch
                {
                }
                string id = Contexts.Current != null ? Contexts.Current.Id : null;
                return id != "options" && id != "save_slots";
            }
        }

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AudioCues.Initialize(gameObject);
            DebugCommands.StateProvider = () => Contexts.DebugState();
            FixedText.AssetName = Game.GameText.NameOfAsset;
            SpeechService.HistoryEnabled = () => GameplayActive;
            // When the language is known already (mod setting, or the game's saved choice), the welcome
            // need not wait for the game's localization to start.
            string early = GameLanguage.EarlyLanguage();
            _languageKnownEarly = early != null;
            Loc.SetLanguage(early ?? "en");
        }

        private bool _languageKnownEarly;
        private bool _firstScreenLogged;
        private bool _labelsRedrawn;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SpeechService.Shutdown();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.Info("Scene loaded: " + scene.name + " (build index " + scene.buildIndex + ", mode " + mode + ")");
            // The very first scene is not always reported as Single; only ignore true additive loads
            // (their objects are looked for again, though).
            if (mode == LoadSceneMode.Additive && _sceneName.Length > 0)
            {
                SceneCache.SceneAdded();
                return;
            }
            _sceneName = scene.name;
            SceneLoadedAt = Time.unscaledTime;
            SceneCache.Clear();
            Contexts.Clear();
            EventAnnouncer.Reset();
            FloorCursor.Reset();
            FloorContext.ToolbarOpen = false;
            _sceneConfiguredAt = -1f;
            if (scene.name == SceneSetup.GameScene)
            {
                // Said already when Continue, New game or a save was chosen in the main menu.
                if (Time.unscaledTime - Patches.LoadingFadePatch.AnnouncedAt > 20f) SpeechService.Say(Loc.Get("loading_game"), true);
            }
            else
            {
                // Back in the menus: the game session's messages and conversation are over.
                SpeechService.History.Clear();
                SpeechService.Recall.Clear();
            }
        }

        // Time the mod spent in this frame's Update (for the perf command).
        private long _frameTicks;

        private void Update()
        {
            long frameStart = Perf.Start();
            try
            {
                long t = Perf.Start();
                Controller.Update();
                ModInput.BeginFrame();
                Perf.Stop("controller", t);
                if (Plugin.Settings.TestHooks.Value) DebugCommands.Poll();
                t = Perf.Start();
                GameLanguage.Update();
                LanguageFiles.Update();
                Perf.Stop("language", t);
                t = Perf.Start();
                Placement.Update();
                Perf.Stop("placement", t);

                // Wait for the player's language (at most 15 seconds) so the welcome is in that language.
                if (!_welcomed && Time.realtimeSinceStartup > 2f && (_languageKnownEarly || GameLanguage.Ready || Time.realtimeSinceStartup > 15f))
                {
                    _welcomed = true;
                    SpeechService.Say(Loc.Get("mod_loaded"), false);
                    // The main menu is announced after the welcome instead of cutting it off.
                    Contexts.GentleNextEntry = true;
                    Log.Info("Welcome spoken " + Time.realtimeSinceStartup.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s after the game started (frame " + Time.frameCount + ")");
                }

                // Configure contexts one frame after the scene loads so every Awake/Start has run.
                if (_sceneConfiguredAt < 0f)
                {
                    _sceneConfiguredAt = Time.unscaledTime;
                    SceneSetup.Configure(Contexts, _sceneName);
                }

                EventSystem es = EventSystem.current;
                if (es != null && es.sendNavigationEvents) es.sendNavigationEvents = false;

                t = Perf.Start();
                TextEntry.Update();
                Game.GameQuirks.Update();
                Perf.Stop("quirks", t);
                t = Perf.Start();
                if (InGame) EventAnnouncer.Update();
                Perf.Stop("events", t);
                t = Perf.Start();
                bool consumed = !TextEntry.Active && GlobalKeys.Handle(this);
                Perf.Stop("keys", t);
                if (consumed) return;
                // No screen is announced before the welcome, so it is in the player's language and first;
                // the first screen also waits for the game's own text (its labels are English until the
                // game's localization has started), at most until 15 seconds after the start.
                if (!_welcomed) return;
                if (!_firstScreenLogged && !(GameLanguage.Ready && (!LanguageFiles.GameTextPending || GameLanguage.SelectedIsComplete)) && Time.realtimeSinceStartup < 15f) return;
                if (!_firstScreenLogged && !_labelsRedrawn && GameLanguage.StartupWorkDeferred)
                {
                    // The English fallback and the templates wait a moment, but the game's own tables and
                    // the labels of the first screen must already be in the player's language when it is read.
                    _labelsRedrawn = true;
                    LanguageFiles.RefreshGameText();
                }
                Contexts.Update();
                if (!_firstScreenLogged && Contexts.Current != null)
                {
                    _firstScreenLogged = true;
                    Log.Info("First screen (" + Contexts.Current.Id + ") " + Time.realtimeSinceStartup.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s after the game started");
                }
            }
            catch (Exception e)
            {
                Log.Error("Update failed: " + e);
            }
            finally
            {
                if (frameStart != 0) _frameTicks = System.Diagnostics.Stopwatch.GetTimestamp() - frameStart;
            }
        }

        private void LateUpdate()
        {
            long start = Perf.Start();
            InputGuard.LateUpdate();
            SpeechService.Update(Time.realtimeSinceStartup);
            if (start != 0)
            {
                long late = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                Perf.Stop("speech", start);
                Perf.EndFrame(_frameTicks + late);
                _frameTicks = 0;
            }
        }

        private void OnApplicationQuit()
        {
            SpeechService.Shutdown();
        }
    }
}
