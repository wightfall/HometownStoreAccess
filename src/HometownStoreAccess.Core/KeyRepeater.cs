namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Turns a held digital input (controller D-pad, stick pushed past a threshold) into discrete
    /// "pressed" events with an initial delay and a repeat rate, like a keyboard's autorepeat.
    /// </summary>
    public sealed class KeyRepeater
    {
        private bool _wasDown;
        private double _nextRepeat;

        public KeyRepeater(double initialDelay = 0.4, double repeatInterval = 0.12)
        {
            InitialDelay = initialDelay;
            RepeatInterval = repeatInterval;
        }

        public double InitialDelay { get; set; }
        public double RepeatInterval { get; set; }

        /// <summary>Returns true when a press event should fire for this frame.</summary>
        public bool Update(bool isDown, double now)
        {
            if (!isDown)
            {
                _wasDown = false;
                return false;
            }
            if (!_wasDown)
            {
                _wasDown = true;
                _nextRepeat = now + InitialDelay;
                return true;
            }
            if (now >= _nextRepeat)
            {
                _nextRepeat = now + RepeatInterval;
                return true;
            }
            return false;
        }

        public void Reset() => _wasDown = false;
    }
}
