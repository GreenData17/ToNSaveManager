namespace ToNSaveManager.Utils {
    /// <summary>
    /// Works like the WinForms timer: Tick runs on the thread that called Start and ticks never overlap.
    /// </summary>
    internal sealed class ContextTimer {
        private readonly Timer m_Timer;
        private SynchronizationContext? m_Context;
        private bool m_Enabled;

        public int Interval { get; set; } = 100;
        public event EventHandler? Tick;

        public ContextTimer() {
            m_Timer = new Timer(OnTimer);
        }

        public void Start() {
            m_Context = SynchronizationContext.Current;
            m_Enabled = true;
            m_Timer.Change(Interval, Timeout.Infinite);
        }

        public void Stop() {
            m_Enabled = false;
            m_Timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimer(object? state) {
            if (m_Context == null) Fire(null);
            else m_Context.Post(Fire, null);
        }

        private void Fire(object? state) {
            // Stopped while the tick was queued
            if (!m_Enabled) return;

            try {
                Tick?.Invoke(this, EventArgs.Empty);
            } finally {
                if (m_Enabled) m_Timer.Change(Interval, Timeout.Infinite);
            }
        }
    }
}
