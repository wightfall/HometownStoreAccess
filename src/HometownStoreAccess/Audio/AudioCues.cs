using System;
using System.Collections.Generic;
using UnityEngine;

namespace HometownStoreAccess.Audio
{
    internal enum Cue
    {
        Move,
        Wrap,
        Edge,
        Activate,
        Error,
        PlaceOk,
        PlaceBlocked,
        CursorTick,
        BuildingHere,
        CustomerArrive,
        CustomerAngry,
        Step,
        Arrive,
        Beacon,
        Open,
        Close,
    }

    /// <summary>
    /// Small synthesized earcons for navigation and spatial feedback. Generated at start-up so the
    /// mod ships no sound files. A pool of 2D sources allows overlapping, independently panned cues.
    /// </summary>
    internal static class AudioCues
    {
        private const int SampleRate = 44100;
        private const int PoolSize = 8;

        private static readonly Dictionary<Cue, AudioClip> Clips = new Dictionary<Cue, AudioClip>();
        private static readonly List<AudioSource> Pool = new List<AudioSource>();
        private static int _next;

        public static void Initialize(GameObject host)
        {
            if (Pool.Count > 0) return;
            for (int i = 0; i < PoolSize; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.ignoreListenerPause = true;
                src.ignoreListenerVolume = false;
                src.bypassEffects = true;
                src.priority = 0;
                Pool.Add(src);
            }
            try
            {
                Clips[Cue.Move] = Tone("move", 0.028, Note(880), 0.25, Wave.Sine);
                Clips[Cue.Wrap] = Sequence("wrap", new[] { Note(1175), Note(784) }, 0.045, 0.3);
                Clips[Cue.Edge] = Tone("edge", 0.07, 140, 0.6, Wave.Triangle);
                Clips[Cue.Activate] = Sequence("activate", new[] { Note(660), Note(990) }, 0.035, 0.3);
                Clips[Cue.Error] = Tone("error", 0.18, 196, 0.35, Wave.Square);
                Clips[Cue.PlaceOk] = Sequence("placeok", new[] { Note(523), Note(659), Note(784) }, 0.06, 0.35);
                Clips[Cue.PlaceBlocked] = Sequence("placeblocked", new[] { Note(330), Note(220) }, 0.08, 0.4);
                Clips[Cue.CursorTick] = Tone("tick", 0.018, 1200, 0.3, Wave.Sine);
                Clips[Cue.BuildingHere] = Tone("building", 0.06, 520, 0.45, Wave.Triangle);
                Clips[Cue.CustomerArrive] = Sequence("arrive_customer", new[] { Note(1319), Note(988) }, 0.12, 0.3);
                Clips[Cue.CustomerAngry] = Tone("angry", 0.22, 150, 0.35, Wave.Square, vibrato: 12);
                Clips[Cue.Step] = Tone("step", 0.03, 700, 0.25, Wave.Triangle);
                Clips[Cue.Arrive] = Sequence("arrive", new[] { Note(784), Note(1047), Note(1319) }, 0.07, 0.3);
                Clips[Cue.Beacon] = Tone("beacon", 0.12, 1568, 0.35, Wave.Sine);
                Clips[Cue.Open] = Sequence("open", new[] { Note(587), Note(880) }, 0.04, 0.25);
                Clips[Cue.Close] = Sequence("close", new[] { Note(880), Note(587) }, 0.04, 0.25);
            }
            catch (Exception e)
            {
                Log.Error("Generating audio cues failed: " + e);
            }
        }

        /// <param name="pan">-1 (left) .. 1 (right).</param>
        /// <param name="pitch">Playback pitch multiplier (used to encode height / distance).</param>
        public static void Play(Cue cue, float pan = 0f, float pitch = 1f, float volume = 1f)
        {
            if (Pool.Count == 0 || Plugin.Settings == null || !Plugin.Settings.AudioCues.Value) return;
            if (IsMenuCue(cue) && !Plugin.Settings.MenuSounds.Value) return;
            PlayClip(cue, pan, pitch, volume);
        }

        /// <summary>Plays a cue for the key list or the volume settings, even if cues are switched off.</summary>
        public static void Preview(Cue cue)
        {
            if (Pool.Count == 0 || Plugin.Settings == null) return;
            PlayClip(cue, 0f, 1f, 1f);
        }

        private static void PlayClip(Cue cue, float pan, float pitch, float volume)
        {
            if (!Clips.TryGetValue(cue, out AudioClip clip) || clip == null) return;
            AudioSource src = Pool[_next];
            _next = (_next + 1) % Pool.Count;
            if (src == null) return;
            src.Stop();
            src.clip = clip;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            src.volume = Mathf.Clamp01(volume * Plugin.Settings.AudioCueVolume.Value * Plugin.Settings.CueVolume(cue).Value);
            src.Play();
        }

        /// <summary>Localized name and purpose of a cue, for the key list and settings.</summary>
        public static string Name(Cue cue) => Core.Loc.Get("cue_" + cue.ToString().ToLowerInvariant());

        private static bool IsMenuCue(Cue c) => c == Cue.Move || c == Cue.Wrap || c == Cue.Edge || c == Cue.Activate || c == Cue.Open || c == Cue.Close;

        private enum Wave { Sine, Square, Triangle }

        private static double Note(double hz) => hz;

        private static AudioClip Tone(string name, double seconds, double hz, double amp, Wave wave, double vibrato = 0)
        {
            int n = Math.Max(1, (int)(seconds * SampleRate));
            var data = new float[n];
            Render(data, 0, n, hz, amp, wave, vibrato);
            return Make(name, data);
        }

        private static AudioClip Sequence(string name, double[] notes, double noteSeconds, double amp)
        {
            int per = Math.Max(1, (int)(noteSeconds * SampleRate));
            var data = new float[per * notes.Length];
            for (int i = 0; i < notes.Length; i++) Render(data, i * per, per, notes[i], amp, Wave.Sine, 0);
            return Make(name, data);
        }

        private static void Render(float[] data, int offset, int count, double hz, double amp, Wave wave, double vibrato)
        {
            double phase = 0;
            int attack = Math.Min(count / 4, (int)(0.004 * SampleRate));
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                double f = vibrato > 0 ? hz * (1 + 0.04 * Math.Sin(2 * Math.PI * vibrato * t)) : hz;
                phase += 2 * Math.PI * f / SampleRate;
                double s;
                switch (wave)
                {
                    case Wave.Square: s = Math.Sign(Math.Sin(phase)) * 0.6; break;
                    case Wave.Triangle: s = 2 / Math.PI * Math.Asin(Math.Sin(phase)); break;
                    default: s = Math.Sin(phase); break;
                }
                // Short attack, exponential decay: clean, click-free earcons.
                double env = i < attack ? (double)i / Math.Max(1, attack) : Math.Exp(-4.0 * (i - attack) / Math.Max(1, count - attack));
                data[offset + i] = (float)(s * env * amp);
            }
        }

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create("hsa_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
