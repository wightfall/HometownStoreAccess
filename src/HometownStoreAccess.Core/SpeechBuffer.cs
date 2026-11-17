using System;
using System.Collections.Generic;
using System.Text;

namespace HometownStoreAccess.Core
{
    /// <summary>One utterance ready to be sent to the screen reader.</summary>
    public struct Utterance
    {
        public string Text;
        public bool Interrupt;

        public Utterance(string text, bool interrupt)
        {
            Text = text;
            Interrupt = interrupt;
        }
    }

    /// <summary>
    /// Collects everything the mod wants to say during a single frame and turns it into one
    /// utterance, so that "panel opened" and "focused item" are not cut off by each other.
    /// Also suppresses accidental repeats of the exact same message in quick succession and
    /// keeps a history the player can review.
    /// </summary>
    public sealed class SpeechBuffer
    {
        private readonly List<string> _pending = new List<string>();
        private bool _pendingInterrupt;
        private readonly LinkedList<string> _history = new LinkedList<string>();
        private string _lastFlushed;
        private double _lastFlushTime = double.NegativeInfinity;

        public SpeechBuffer(int historyLimit = 100, double repeatWindowSeconds = 0.25)
        {
            HistoryLimit = Math.Max(1, historyLimit);
            RepeatWindowSeconds = repeatWindowSeconds;
        }

        public int HistoryLimit { get; }
        public double RepeatWindowSeconds { get; set; }
        public string LastSpoken { get; private set; } = string.Empty;
        public bool HasPending => _pending.Count > 0;

        /// <summary>Most recent first.</summary>
        public IReadOnlyCollection<string> History => _history;

        /// <summary>
        /// Queues text. Everything queued in one frame is spoken together, in order; the interrupt
        /// flag only decides whether the screen reader drops what it was already saying.
        /// </summary>
        public void Add(string text, bool interrupt)
        {
            string clean = TextCleaner.Clean(text);
            if (clean.Length == 0) return;
            if (_pending.Count > 0 && string.Equals(_pending[_pending.Count - 1], clean, StringComparison.Ordinal)) return;
            _pending.Add(clean);
            if (interrupt) _pendingInterrupt = true;
        }

        /// <summary>Drops the queued text (used when a newer, more relevant message supersedes it).</summary>
        public void ClearPending()
        {
            _pending.Clear();
            _pendingInterrupt = false;
        }

        /// <summary>
        /// Produces the combined utterance for this frame, or null when there is nothing to say or
        /// the text is an immediate duplicate of the previous utterance.
        /// </summary>
        public Utterance? Flush(double now)
        {
            if (_pending.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (string p in _pending)
            {
                if (sb.Length > 0)
                {
                    char last = sb[sb.Length - 1];
                    if (!TextCleaner.EndsWithPunctuation(sb.ToString())) sb.Append('.');
                    sb.Append(' ');
                }
                sb.Append(p);
            }
            bool interrupt = _pendingInterrupt;
            _pending.Clear();
            _pendingInterrupt = false;

            string text = sb.ToString();
            if (string.Equals(text, _lastFlushed, StringComparison.Ordinal) && now - _lastFlushTime < RepeatWindowSeconds)
            {
                return null;
            }
            _lastFlushed = text;
            _lastFlushTime = now;
            LastSpoken = text;
            _history.AddFirst(text);
            while (_history.Count > HistoryLimit) _history.RemoveLast();
            return new Utterance(text, interrupt);
        }

        /// <summary>Returns the history entry at <paramref name="index"/> (0 = newest) or null.</summary>
        public string HistoryAt(int index)
        {
            if (index < 0 || index >= _history.Count) return null;
            int i = 0;
            foreach (string s in _history)
            {
                if (i == index) return s;
                i++;
            }
            return null;
        }
    }
}
