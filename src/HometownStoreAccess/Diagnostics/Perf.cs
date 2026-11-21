using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>
    /// Developer tool (the "perf" test command): how long the mod's parts take per frame. Costs nothing
    /// while no measurement runs.
    /// </summary>
    internal static class Perf
    {
        private sealed class Stat
        {
            public long Ticks;
            public long Max;
            public int Calls;
        }

        private static readonly Dictionary<string, Stat> Stats = new Dictionary<string, Stat>(StringComparer.Ordinal);
        private static float _until;
        private static int _frames;
        private static long _frameTicks;
        private static long _frameMax;
        private static int _gcStart;
        private static int _searchesStart;

        public static bool Enabled { get; private set; }

        /// <summary>Measures for the given number of seconds, then writes the report to the log.</summary>
        public static void Begin(float seconds)
        {
            Stats.Clear();
            _frames = 0;
            _frameTicks = _frameMax = 0;
            _gcStart = GC.CollectionCount(0);
            _searchesStart = UI.SceneCache.Searches;
            _until = UnityEngine.Time.unscaledTime + Math.Max(1f, seconds);
            Enabled = true;
            Log.Info("[perf] measuring for " + seconds + " s");
        }

        public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

        public static void Stop(string name, long start)
        {
            if (!Enabled || start == 0) return;
            long d = Stopwatch.GetTimestamp() - start;
            if (!Stats.TryGetValue(name, out Stat s)) Stats[name] = s = new Stat();
            s.Ticks += d;
            s.Calls++;
            if (d > s.Max) s.Max = d;
        }

        /// <summary>Called once per frame with the time of the mod's whole Update and LateUpdate.</summary>
        public static void EndFrame(long frameTicks)
        {
            if (!Enabled) return;
            _frames++;
            _frameTicks += frameTicks;
            if (frameTicks > _frameMax) _frameMax = frameTicks;
            if (UnityEngine.Time.unscaledTime >= _until) Report();
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static void Report()
        {
            Enabled = false;
            int frames = Math.Max(1, _frames);
            var sb = new StringBuilder();
            sb.Append("[perf] ").Append(_frames).Append(" frames; mod per frame ")
              .Append(Ms(_frameTicks / frames).ToString("0.000")).Append(" ms (max ").Append(Ms(_frameMax).ToString("0.00")).Append(" ms); ")
              .Append(GC.CollectionCount(0) - _gcStart).Append(" garbage collections; ")
              .Append(UI.SceneCache.Searches - _searchesStart).Append(" scene searches");
            Log.Info(sb.ToString());
            foreach (var kv in Stats.OrderByDescending(k => k.Value.Ticks).Take(25))
            {
                Log.Info("[perf]   " + kv.Key + ": " + Ms(kv.Value.Ticks / frames).ToString("0.0000") + " ms/frame, "
                    + kv.Value.Calls + " calls, max " + Ms(kv.Value.Max).ToString("0.000") + " ms");
            }
        }
    }
}
