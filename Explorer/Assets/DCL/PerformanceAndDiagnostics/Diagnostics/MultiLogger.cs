using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Logs exceptions and errors to a process-specific file.
    ///     Useful for debugging multiple instances running in parallel.
    /// </summary>
    public class MultiLogger : IDisposable
    {
        private readonly StreamWriter writer;
        private bool disposed;

        public MultiLogger()
        {
            int pid = Process.GetCurrentProcess().Id;

            string dir = Application.persistentDataPath;
            Directory.CreateDirectory(dir);

            string logPath = Path.Combine(dir, $"player-{pid}.log");

            writer = new StreamWriter(logPath, append: true);
            writer.AutoFlush = true;

            Application.logMessageReceived += OnLog;

            ReportHub.Log(ReportCategory.ENGINE, $"MultiLogger initialized. Logging to: {logPath}");
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (disposed) return;

            writer.WriteLine($"[{type}] {condition}");

            if (type is LogType.Exception or LogType.Error)
            {
                if (!string.IsNullOrEmpty(stackTrace))
                    writer.WriteLine(stackTrace);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            Application.logMessageReceived -= OnLog;
            writer.Dispose();

            ReportHub.Log(ReportCategory.ENGINE, "MultiLogger disposed.");
        }
    }
}

