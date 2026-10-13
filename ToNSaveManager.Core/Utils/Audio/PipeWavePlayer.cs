using System.Diagnostics;
using NAudio.Wave;

namespace ToNSaveManager.Utils.Audio {
    /// <summary>
    /// Stand-in for WaveOutEvent outside of Windows. Pipes the 32-bit float samples into pw-play or paplay.
    /// </summary>
    internal sealed class PipeWavePlayer : IWavePlayer {
        private IWaveProvider? m_Provider;
        private volatile bool m_Playing;

        public float Volume { get; set; } = 1f;
        public PlaybackState PlaybackState => m_Playing ? PlaybackState.Playing : PlaybackState.Stopped;
        public WaveFormat OutputWaveFormat => m_Provider?.WaveFormat ?? throw new InvalidOperationException("Not initialized.");
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public void Init(IWaveProvider waveProvider) {
            if (waveProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new ArgumentException("Only 32-bit float audio is supported.", nameof(waveProvider));

            m_Provider = waveProvider;
        }

        public void Play() {
            if (m_Playing || m_Provider == null) return;

            m_Playing = true;
            new Thread(PlaybackThread) { IsBackground = true, Name = "PipeWavePlayer" }.Start();
        }

        public void Stop() => m_Playing = false;
        public void Pause() => Stop();
        public void Dispose() => Stop();

        private void PlaybackThread() {
            IWaveProvider provider = m_Provider!;
            WaveFormat format = provider.WaveFormat;

            // 20 ms per write, the blocking pipe keeps the pace
            byte[] buffer = new byte[format.AverageBytesPerSecond / 50 / format.BlockAlign * format.BlockAlign];

            Process? process = null;
            Stream? input = null;
            try {
                process = LinuxAudio.StartPlayer(format.SampleRate, format.Channels);
                input = process?.StandardInput.BaseStream;
            } catch (Exception e) {
                Logger.Error("Failed to start the audio player: " + e);
            }

            while (m_Playing) {
                int read = provider.Read(buffer, 0, buffer.Length);

                if (input == null) {
                    // No player, drop the samples so sounds still finish
                    Thread.Sleep(20);
                    continue;
                }

                try {
                    input.Write(buffer, 0, read);
                } catch (IOException e) {
                    Logger.Error("Audio player stopped: " + e.Message);
                    input = null;
                }
            }

            try {
                input?.Dispose();
                process?.Kill();
            } catch (Exception) {
                // Already exited
            }
            process?.Dispose();

            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }
    }
}
