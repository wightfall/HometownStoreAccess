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
        public bool InGame => _sceneName == SceneSetup.GameScene;

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AudioCues.Initialize(gameObject);
            DebugCommands.StateProvider = () => Contexts.DebugState();
            Loc.SetLanguage(Plugin.Settings.ModLanguage.Value == "Auto" ? "en" : Plugin.Settings.ModLanguage.Value);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SpeechService.Shutdown();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.Info("Scene loaded: " + scene.name + " (build index " + scene.buildIndex + ", mode " + mode + ")");
            // The very first scene is not always reported as Single; only ignore true additive loads.
            if (mode == LoadSceneMode.Additive && _sceneName.Length > 0) return;
            _sceneName = scene.name;
            SceneCache.Clear();
            Contexts.Clear();
            EventAnnouncer.Reset();
            FloorCursor.Reset();
            FloorContext.ToolbarOpen = false;
            _sceneConfiguredAt = -1f;
            if (scene.name == SceneSetup.GameScene) SpeechService.Say(Loc.Get("loading_game"), true);
        }

        private void Update()
        {
            try
            {
                Controller.Update();
                ModInput.BeginFrame();
                if (Plugin.Settings.TestHooks.Value) DebugCommands.Poll();
                GameLanguage.Update();
                LanguageFiles.Update();
                Placement.Update();

                // Wait for the player's language (at most 15 seconds) so the welcome is in that language.
                if (!_welcomed && Time.realtimeSinceStartup > 2f && (GameLanguage.Ready || Time.realtimeSinceStartup > 15f))
                {
                    _welcomed = true;
                    SpeechService.Say(Loc.Get("mod_loaded"), false);
                }

                // Configure contexts one frame after the scene loads so every Awake/Start has run.
                if (_sceneConfiguredAt < 0f)
                {
                    _sceneConfiguredAt = Time.unscaledTime;
                    SceneSetup.Configure(Contexts, _sceneName);
                }

                EventSystem es = EventSystem.current;
                if (es != null && es.sendNavigationEvents) es.sendNavigationEvents = false;

                TextEntry.Update();
                Game.GameQuirks.Update();
                if (InGame) EventAnnouncer.Update();
                if (!TextEntry.Active && GlobalKeys.Handle(this)) return;
                Contexts.Update();
            }
            catch (Exception e)
            {
                Log.Error("Update failed: " + e);
            }
        }

        private void LateUpdate()
        {
            InputGuard.LateUpdate();
            SpeechService.Update(Time.realtimeSinceStartup);
        }

        private void OnApplicationQuit()
        {
            SpeechService.Shutdown();
        }
    }
}
