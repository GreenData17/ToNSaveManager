using System.Diagnostics;

namespace ToNSaveManager.Utils.Audio {
    /// <summary>
    /// Audio outside of Windows: plays through pw-play (PipeWire) or paplay (PulseAudio), both read from stdin.
    /// </summary>
    internal static class LinuxAudio {
        static readonly string? PwPlay = FindInPath("pw-play");
        static readonly string? PaPlay = FindInPath("paplay");
        static bool LoggedMissing;

        /// <returns>The full path of the program, or null if it is not in PATH.</returns>
        internal static string? FindInPath(string name) {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator)) {
                if (string.IsNullOrEmpty(dir)) continue;

                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        internal static Process StartProcess(string fileName, IEnumerable<string> args, bool redirectInput = false, bool redirectOutput = false) {
            ProcessStartInfo info = new ProcessStartInfo(fileName, args) {
                RedirectStandardInput = redirectInput,
                RedirectStandardOutput = redirectOutput,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            return Process.Start(info) ?? throw new InvalidOperationException("Could not start " + fileName);
        }

        /// <summary>Kills the process if it still runs and disposes it.</summary>
        internal static void StopProcess(Process? process) {
            if (process == null) return;

            try {
                if (!process.HasExited) process.Kill();
            } catch (InvalidOperationException) {
                // Already exited
            }
            process.Dispose();
        }

        /// <returns>False if the process is null, exited or disposed.</returns>
        internal static bool IsRunning(Process? process) {
            try {
                return process != null && !process.HasExited;
            } catch (InvalidOperationException) {
                return false;
            }
        }

        /// <summary>
        /// Starts a player that reads a sound file from stdin,
        /// or raw 32-bit float samples if <paramref name="sampleRate"/> and <paramref name="channels"/> are given.
        /// </summary>
        /// <returns>Null if neither pw-play nor paplay is installed.</returns>
        internal static Process? StartPlayer(int sampleRate = 0, int channels = 0) {
            bool raw = sampleRate > 0;

            if (PwPlay != null) {
                List<string> args = raw ? ["--raw", "--format", "f32", "--rate", sampleRate.ToString(), "--channels", channels.ToString()] : [];
                args.Add("-");
                return StartProcess(PwPlay, args, redirectInput: true);
            } else if (PaPlay != null) {
                List<string> args = raw ? ["--raw", "--format=float32le", $"--rate={sampleRate}", $"--channels={channels}"] : [];
                return StartProcess(PaPlay, args, redirectInput: true);
            }

            if (!LoggedMissing) {
                LoggedMissing = true;
                Logger.Warning("Neither 'pw-play' nor 'paplay' was found in PATH, sounds are disabled.");
            }
            return null;
        }
    }
}
