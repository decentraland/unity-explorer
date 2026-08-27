using System;
using System.Diagnostics;

namespace UUAV.Tests
{
    /// <summary>
    /// Locates and kills the out-of-process uuav-helper this Unity instance
    /// spawned, for the crash-recovery suite.
    /// </summary>
    public static class HelperProcess
    {
        private const string HelperName = "uuav-helper";

        /// <summary>
        /// Pid of the running helper, preferring one parented by this
        /// process when several editors run side by side; null when no
        /// helper is alive.
        /// </summary>
        public static int? FindPid()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Process[] candidates = Process.GetProcessesByName(HelperName);
            return candidates.Length > 0 ? candidates[0].Id : null;
#else
            string output = RunAndCapture("/usr/bin/pgrep", $"-x {HelperName}");
            int currentPid = Process.GetCurrentProcess().Id;
            int? first = null;
            foreach (string line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(line.Trim(), out int pid) == false)
                {
                    continue;
                }

                first ??= pid;
                string parent = RunAndCapture("/bin/ps", $"-o ppid= -p {pid}").Trim();
                if (int.TryParse(parent, out int parentPid) && parentPid == currentPid)
                {
                    return pid;
                }
            }

            return first;
#endif
        }

        /// <summary>SIGKILL: simulates a helper crash, no chance to clean up.</summary>
        public static void Kill(int pid)
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }

#if !(UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
        private static string RunAndCapture(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return string.Empty;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
#endif
    }
}
