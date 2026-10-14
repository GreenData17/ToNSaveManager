using System.Diagnostics;

namespace ToNSaveManager.Utils.Audio {
    /// <summary>
    /// Text to speech outside of Windows: espeak-ng, or spd-say (speech-dispatcher) as fallback.
    /// </summary>
    internal sealed class LinuxSpeech {
        static readonly string? ESpeak = LinuxAudio.FindInPath("espeak-ng");
        static readonly string? SpdSay = LinuxAudio.FindInPath("spd-say");
        static bool LoggedMissing;

        private Process? m_Process;

        internal bool IsSpeaking => LinuxAudio.IsRunning(m_Process);

        /// <param name="volume">0 to 100</param>
        internal void Speak(string text, int volume) {
            Stop();

            if (ESpeak != null) {
                // Amplitude 0 to 200, 100 is the default
                m_Process = LinuxAudio.StartProcess(ESpeak, ["-a", volume.ToString(), "--", text]);
            } else if (SpdSay != null) {
                // Volume -100 to 100, 0 is the default. Stop can't interrupt the speech server.
                m_Process = LinuxAudio.StartProcess(SpdSay, ["-w", "-i", (volume - 100).ToString(), "--", text]);
            } else if (!LoggedMissing) {
                LoggedMissing = true;
                Logger.Warning("Neither 'espeak-ng' nor 'spd-say' was found in PATH, text to speech is disabled.");
            }
        }

        internal void Stop() {
            LinuxAudio.StopProcess(m_Process);
            m_Process = null;
        }
    }
}
