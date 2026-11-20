using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>Kinds of spoken messages, each with its own history buffer.</summary>
    public enum MsgCategory
    {
        /// <summary>Every message, including menu navigation.</summary>
        All,
        Customers,
        Sales,
        Dialog,
        Events,
        Shop,
        Tutorial,
        Notices,
    }

    /// <summary>
    /// History of spoken messages grouped in categories, for review with , and . (messages) and
    /// [ and ] (categories). Each category remembers its own reading position.
    /// </summary>
    public sealed class MessageLog
    {
        private readonly Dictionary<MsgCategory, List<string>> _buffers = new Dictionary<MsgCategory, List<string>>();
        private readonly Dictionary<MsgCategory, int> _positions = new Dictionary<MsgCategory, int>();
        private readonly int _limit;

        public static readonly MsgCategory[] Categories = (MsgCategory[])Enum.GetValues(typeof(MsgCategory));

        public MessageLog(int limitPerCategory = 300)
        {
            _limit = Math.Max(1, limitPerCategory);
            foreach (MsgCategory c in Categories)
            {
                _buffers[c] = new List<string>();
                _positions[c] = -1;
            }
        }

        public MsgCategory Current { get; private set; } = MsgCategory.All;

        /// <summary>Forgets every message (a game session ended).</summary>
        public void Clear()
        {
            foreach (MsgCategory c in Categories)
            {
                _buffers[c].Clear();
                _positions[c] = -1;
            }
            Current = MsgCategory.All;
        }

        public int Count(MsgCategory c) => _buffers[c].Count;

        /// <summary>Records a message in "All" and, if different, in its own category. Reading positions follow the newest.</summary>
        public void Add(string text, MsgCategory category)
        {
            if (string.IsNullOrEmpty(text)) return;
            Append(MsgCategory.All, text);
            if (category != MsgCategory.All) Append(category, text);
        }

        private void Append(MsgCategory c, string text)
        {
            List<string> b = _buffers[c];
            b.Add(text);
            if (b.Count > _limit) b.RemoveAt(0);
            _positions[c] = b.Count - 1;
        }

        /// <summary>Older (-1) or newer (+1) message in the current category; null when empty. Stops at the ends.</summary>
        public string Step(int direction, out bool atEdge)
        {
            List<string> b = _buffers[Current];
            atEdge = false;
            if (b.Count == 0) return null;
            int p = _positions[Current];
            if (p < 0 || p >= b.Count) p = b.Count - 1;
            int np = p + Math.Sign(direction);
            if (np < 0 || np >= b.Count)
            {
                atEdge = true;
                np = p;
            }
            _positions[Current] = np;
            return b[np];
        }

        /// <summary>Oldest (first) or newest (last) message of the current category.</summary>
        public string Jump(bool toLast)
        {
            List<string> b = _buffers[Current];
            if (b.Count == 0) return null;
            int p = toLast ? b.Count - 1 : 0;
            _positions[Current] = p;
            return b[p];
        }

        public int Position => _positions[Current];

        /// <summary>Changes category; wraps around. Returns the new category.</summary>
        public MsgCategory CycleCategory(int direction)
        {
            int i = Array.IndexOf(Categories, Current);
            int n = Categories.Length;
            Current = Categories[((i + Math.Sign(direction)) % n + n) % n];
            return Current;
        }

        public MsgCategory JumpCategory(bool toLast)
        {
            Current = toLast ? Categories[Categories.Length - 1] : Categories[0];
            return Current;
        }

        public string LastIn(MsgCategory c)
        {
            List<string> b = _buffers[c];
            return b.Count == 0 ? null : b[b.Count - 1];
        }
    }
}
