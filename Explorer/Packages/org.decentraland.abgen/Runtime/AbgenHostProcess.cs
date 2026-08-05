using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Decentraland.Abgen
{
    /// <summary>
    /// Converts through a spawned <c>abgen-host</c> instead of the in-process
    /// library: a hostile glb kills the helper, not the client. Costs a spawn
    /// plus a copy each way, so prefer it for content you did not author.
    /// </summary>
    public static class AbgenHostProcess
    {
        private const uint FrameDone = 0xFFFFFFFFu;

        /// <summary>Deployed next to the native library.</summary>
        public static string ExecutableName =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            "abgen-host.exe";
#else
            "abgen-host";
#endif

        /// <summary>Runs one conversion in a child process.</summary>
        /// <param name="maxMemoryMb">
        /// Applied by the helper itself: <c>RLIMIT_AS</c> on Linux, a
        /// job-object commit limit on Windows, refused on macOS. 0 is
        /// uncapped. Setting it also bounds the child's worker pool, whose
        /// stacks count against the same limit.
        /// </param>
        /// <param name="threads">Worker cap; 0 leaves the default.</param>
        /// <param name="timeout"><see cref="TimeSpan.Zero"/> waits forever.</param>
        public static AbgenResult Convert(
            string executablePath,
            AbgenRequest request,
            uint maxMemoryMb = 0,
            uint threads = 0,
            TimeSpan timeout = default)
        {
            if (executablePath == null) throw new ArgumentNullException(nameof(executablePath));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var args = new List<string>();
            if (maxMemoryMb > 0)
            {
                args.Add("--max-memory-mb");
                args.Add(maxMemoryMb.ToString());
            }
            if (threads > 0)
            {
                args.Add("--threads");
                args.Add(threads.ToString());
            }

            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var result = new AbgenResult();
            byte[] blob = request.ToBytes();

            using var proc = new Process { StartInfo = psi };
            var stderr = new StringBuilder();

            proc.Start();

            var drain = new Thread(() =>
            {
                try { stderr.Append(proc.StandardError.ReadToEnd()); }
                catch (Exception) {  }
            }) { IsBackground = true };
            drain.Start();

            try
            {
                Stream stdin = proc.StandardInput.BaseStream;
                WriteUInt32(stdin, (uint)blob.Length);
                stdin.Write(blob, 0, blob.Length);
                stdin.Flush();
                stdin.Close();

                Task reader = Task.Run(() => ReadFrames(proc.StandardOutput.BaseStream, result));
                bool finished = timeout == default || reader.Wait(timeout);

                if (!finished)
                {
                    TryKill(proc);
                    reader.Wait(TimeSpan.FromSeconds(5));
                    result.Status = AbgenStatus.ConvertFailed;
                    result.Errors.Add($"abgen-host timed out after {timeout}");
                    return result;
                }

                if (timeout == default) reader.Wait();
                proc.WaitForExit();

                if (result.Status == AbgenStatus.Ok && proc.ExitCode != 0
                    && result.Errors.Count == 0)
                {
                    drain.Join(TimeSpan.FromSeconds(2));
                    result.Status = AbgenStatus.ConvertFailed;
                    string tail = stderr.ToString();
                    result.Errors.Add(
                        $"abgen-host exited with {proc.ExitCode}"
                        + (tail.Length > 0 ? $": {tail.Trim()}" : " and no diagnostics"));
                }
            }
            catch (IOException e)
            {
                TryKill(proc);
                result.Status = AbgenStatus.ConvertFailed;
                result.Errors.Add($"abgen-host pipe failed: {e.Message}");
            }
            finally
            {
                drain.Join(TimeSpan.FromSeconds(1));
            }

            return result;
        }

        private static void TryKill(Process proc)
        {
            try { if (!proc.HasExited) proc.Kill(); }
            catch (Exception) {  }
        }

        private static void ReadFrames(Stream stdout, AbgenResult result)
        {
            while (true)
            {
                if (!TryReadExact(stdout, 8, out byte[] head)) return;

                uint kind = BitConverter.ToUInt32(head, 0);
                if (kind == FrameDone)
                {
                    result.Status = (AbgenStatus)BitConverter.ToInt32(head, 4);
                    return;
                }

                uint len = BitConverter.ToUInt32(head, 4);
                if (len > int.MaxValue) return;

                byte[] payload = Array.Empty<byte>();
                if (len > 0 && !TryReadExact(stdout, (int)len, out payload)) return;

                switch ((AbgenKind)kind)
                {
                    case AbgenKind.Json:
                        result.Events.Add(Encoding.UTF8.GetString(payload));
                        break;
                    case AbgenKind.Output:
                        if (AbgenConverter.TrySplitOutput(payload, out string name, out byte[] data))
                            result.Artifacts.Add(new AbgenArtifact(name, data));
                        break;
                    case AbgenKind.Error:
                        result.Errors.Add(Encoding.UTF8.GetString(payload));
                        break;
                    case AbgenKind.Manifest:
                        result.Manifest = Encoding.UTF8.GetString(payload);
                        break;
                }
            }
        }

        /// <summary>Pipes deliver short reads; one Read truncates a frame.</summary>
        private static bool TryReadExact(Stream s, int count, out byte[] buf)
        {
            buf = new byte[count];
            int off = 0;
            while (off < count)
            {
                int n = s.Read(buf, off, count - off);
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        private static void WriteUInt32(Stream s, uint v)
        {
            s.Write(BitConverter.GetBytes(v), 0, 4);
        }
    }
}
