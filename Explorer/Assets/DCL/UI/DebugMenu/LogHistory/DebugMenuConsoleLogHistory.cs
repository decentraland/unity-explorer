using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.UI.DebugMenu.LogHistory
{
    public class DebugMenuConsoleLogHistory
    {
        public readonly List<DebugMenuConsoleLogEntry> FilteredLogMessages = new ();
        public event Action LogsUpdated;
        public bool Paused { get; set; }
        public int LogEntryCount => allLogMessages.Count(logEntry => logEntry.Type == LogMessageType.Log);
        public int ErrorEntryCount => allLogMessages.Count(logEntry => logEntry.Type == LogMessageType.Error);

        private readonly List<DebugMenuConsoleLogEntry> allLogMessages = new ();
        private string textFilter;
        private bool showErrorEntries = true;
        private bool showLogEntries = true;

        public void AddLogMessage(DebugMenuConsoleLogEntry logEntry)
        {
            if (Paused) return;

            allLogMessages.Add(logEntry);

            if (!KeepAfterFilter(logEntry)) return;

            FilteredLogMessages.Add(logEntry);
            LogsUpdated?.Invoke();
        }

        public void ClearLogMessages()
        {
            allLogMessages.Clear();
            FilteredLogMessages.Clear();
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
