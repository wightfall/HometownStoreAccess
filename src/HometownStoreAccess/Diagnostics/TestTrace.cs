using System.Collections.Generic;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>
    /// For the test commands only (TestHooks on): the sound cues played and the speech sent since the last
    /// "state", so a test can tell what one key press did without waiting for the log to be written.
    /// </summary>
    internal static class TestTrace
    {
        private static readonly List<string> Cues = new List<string>();
        private static readonly List<string> Said = new List<string>();

        private static bool On => Plugin.Settings != null && Plugin.Settings.TestHooks.Value;

        public static void Cue(string name)
        {
            if (On && Cues.Count < 50) Cues.Add(name);
        }

        public static void Speech(string text)
        {
            if (On && Said.Count < 50) Said.Add(text);
        }

        /// <summary>"cues=Move,Edge | said=..." and starts over.</summary>
        public static string Take()
        {
            string r = "cues=" + string.Join(",", Cues.ToArray()) + " | said=" + string.Join(" / ", Said.ToArray());
            Cues.Clear();
            Said.Clear();
            return r;
        }
    }
}
