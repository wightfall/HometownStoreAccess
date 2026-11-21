using System;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// The task lines of the game's event journal. The game strikes a task through (&lt;s&gt;) once it is
    /// done; the strike-through is invisible to speech, so the mod says "done" instead.
    /// </summary>
    public static class JournalTasks
    {
        /// <summary>True when the game marked the task as done (struck through).</summary>
        public static bool IsDone(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            return raw.IndexOf("<s>", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("<strikethrough>", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The task as spoken: the text without markup, with "done" when <paramref name="markDone"/> and it is done.</summary>
        public static string Describe(string raw, bool markDone)
        {
            string clean = TextCleaner.Clean(raw ?? string.Empty);
            if (clean.Length == 0) return clean;
            return markDone && IsDone(raw) ? Loc.Get("task_done", clean) : clean;
        }
    }
}
