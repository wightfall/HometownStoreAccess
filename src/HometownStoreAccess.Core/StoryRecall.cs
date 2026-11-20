using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Remembers the latest conversation (all its lines) and the latest tutorial message, so they can
    /// be heard again with one key from any screen instead of searching the message history.
    /// </summary>
    public sealed class StoryRecall
    {
        private readonly List<string> _conversation = new List<string>();
        private readonly int _limit;
        private bool _started;

        public StoryRecall(int limit = 200)
        {
            _limit = limit;
        }

        /// <summary>The newest conversation line or tutorial message.</summary>
        public string Last { get; private set; }

        /// <summary>The lines of the current (or last) conversation, oldest first.</summary>
        public IReadOnlyList<string> Conversation => _conversation;

        /// <summary>A new conversation starts: the next line replaces the previous conversation.</summary>
        public void StartConversation() => _started = true;

        public void AddLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_started)
            {
                _conversation.Clear();
                _started = false;
            }
            // The same line can be announced again (typing finished, screen re-entered): keep it once.
            if (_conversation.Count == 0 || _conversation[_conversation.Count - 1] != line)
            {
                _conversation.Add(line);
                if (_conversation.Count > _limit) _conversation.RemoveAt(0);
            }
            Last = line;
        }

        public void AddTutorial(string text)
        {
            if (!string.IsNullOrEmpty(text)) Last = text;
        }

        public void Clear()
        {
            _conversation.Clear();
            _started = false;
            Last = null;
        }
    }
}
