using System.Diagnostics;
using NAudio.Wave;

namespace ToNSaveManager.Utils.Audio {
    /// <summary>
    /// Stand-in for MediaFoundationReader outside of Windows. Decodes a file or URL with ffmpeg
    /// into 32-bit float stereo at 44.1 kHz. Can not seek and has no length.
    /// </summary>
    internal sealed class FFmpegStream : WaveStream {
        private readonly Process m_Process;
        private readonly Stream m_Output;
        private long m_Position;

        public FFmpegStream(string input) {
            string ffmpeg = LinuxAudio.FindInPath("ffmpeg") ?? throw new FileNotFoundException("'ffmpeg' was not found in PATH.");

            m_Process = LinuxAudio.StartProcess(ffmpeg, ["-nostdin", "-v", "error", "-i", input, "-f", "f32le", "-ac", "2", "-ar", "44100", "-"], redirectOutput: true);
            m_Output = m_Process.StandardOutput.BaseStream;
        }

        public override WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public override long Length => 0;

        public override long Position {
            get => m_Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            // Fill the whole buffer, the mixer drops inputs that return less
            int total = 0;
            while (total < count) {
                int read = m_Output.Read(buffer, offset + total, count - total);
                if (read == 0) break;
                total += read;
            }

            m_Position += total;
            return total;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                try {
                    if (!m_Process.HasExited) m_Process.Kill();
                } catch (InvalidOperationException) {
                    // Already exited
                }
                m_Process.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
