using System.Diagnostics;
using System.Media;

namespace ToNSaveManager.Utils.Audio {
    /// <summary>
    /// Plays one WAV sound at a time. A SoundPlayer on Windows, a pw-play or paplay process elsewhere.
    /// </summary>
    internal sealed class SoundChannel {
        private SoundPlayer? m_Player;
        private Process? m_Process;

        internal void Stop() {
            if (OperatingSystem.IsWindows()) {
                m_Player?.Stop();
                return;
            }

            LinuxAudio.StopProcess(m_Process);
            m_Process = null;
        }

        internal void Play(string fileName) {
            if (OperatingSystem.IsWindows()) {
                m_Player ??= new SoundPlayer();
                m_Player.Stop();
                m_Player.SoundLocation = fileName;
                m_Player.Play();
                return;
            }

            Play(File.ReadAllBytes(fileName));
        }

        internal void Play(Stream stream) {
            if (OperatingSystem.IsWindows()) {
                m_Player ??= new SoundPlayer();
                m_Player.Stop();
                m_Player.Stream = null;

                stream.Position = 0;
                m_Player.Stream = stream;

                m_Player.Play();
                return;
            }

            using MemoryStream copy = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(copy);
            Play(copy.ToArray());
        }

        private void Play(byte[] data) {
            Stop();

            Process? process = LinuxAudio.StartPlayer();
            if (process == null) return;
            m_Process = process;

            // The pipe only holds a part of the sound, so write in the background
            Task.Run(() => {
                try {
                    using Stream input = process.StandardInput.BaseStream;
                    input.Write(data);
                } catch (Exception e) when (e is IOException or ObjectDisposedException) {
                    // Player was stopped
                }
            });
        }
    }
}
