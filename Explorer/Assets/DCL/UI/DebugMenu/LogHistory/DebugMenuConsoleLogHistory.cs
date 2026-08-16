using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.UI.DebugMenu.LogHistory
{
    /// <summary>
    ///     <see cref="AddLogMessage" /> is fed from the global log callback and may be invoked from any
    ///     thread; it only enqueues into a capped pending queue. Every other member — the lists, the
    ///     counters and <see cref="LogsUpdated" /> — is main-thread-only: entries become visible when
    ///     <see cref="DrainPendingLogs" /> runs on the main thread.
    /// </summary>
    public class DebugMenuConsoleLogHistory
    {
        private const int MAX_PENDING_LOGS = 10000;

        public readonly List<DebugMenuConsoleLogEntry> FilteredLogMessages = new ();
        public event Action? LogsUpdated;
        public bool Paused { get; set; }
        public int LogEntryCount { get; private set; }
        public int ErrorEntryCount { get; private set; }

        private readonly object pendingLock = new ();
        private readonly Queue<DebugMenuConsoleLogEntry> pendingLogMessages = new ();
        private readonly List<DebugMenuConsoleLogEntry> drainBuffer = new ();
        private readonly List<DebugMenuConsoleLogEntry> allLogMessages = new ();
        private string? textFilter;
        private bool showErrorEntries = true;
        private bool showLogEntries = true;

        public void AddLogMessage(DebugMenuConsoleLogEntry logEntry)
        {
            if (Paused) return;

            lock (pendingLock)
            {
                if (pendingLogMessages.Count == MAX_PENDING_LOGS)
                    pendingLogMessages.Dequeue();

                pendingLogMessages.Enqueue(logEntry);
            }
        }

        public void DrainPendingLogs()
        {
            lock (pendingLock)
            {
                while (pendingLogMessages.Count > 0)
                    drainBuffer.Add(pendingLogMessages.Dequeue());
            }

            if (drainBuffer.Count == 0) return;

            for (var i = 0; i < drainBuffer.Count; i++)
            {
                DebugMenuConsoleLogEntry logEntry = drainBuffer[i];

                allLogMessages.Add(logEntry);

                if (logEntry.Type == LogMessageType.Log)
                    LogEntryCount++;
                else if (logEntry.Type == LogMessageType.Error)
                    ErrorEntryCount++;

                if (KeepAfterFilter(logEntry))
                    FilteredLogMessages.Add(logEntry);
            }

            drainBuffer.Clear();
            LogsUpdated?.Invoke();
        }

        public void ClearLogMessages()
        {
            lock (pendingLock) { pendingLogMessages.Clear(); }

            allLogMessages.Clear();
            FilteredLogMessages.Clear();
            LogEntryCount = 0;
            ErrorEntryCount = 0;
            LogsUpdated?.Invoke();
        }

        public void ApplyFilter(string targetText, bool showErrors, bool showLogs)
        {
            showErrorEntries = showErrors;
            showLogEntries = showLogs;
            textFilter = targetText;
            FilteredLogMessages.Clear();

            // Where() is used instead of List.FindAll() to decrease allocations
            FilteredLogMessages.AddRange(allLogMessages.Where(KeepAfterFilter));

            LogsUpdated?.Invoke();
        }

        private bool KeepAfterFilter(DebugMenuConsoleLogEntry entry) =>
            (string.IsNullOrEmpty(textFilter) || entry.Message.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
            && (showErrorEntries || entry.Type != LogMessageType.Error)
            && (showLogEntries || entry.Type != LogMessageType.Log);
    }
}
