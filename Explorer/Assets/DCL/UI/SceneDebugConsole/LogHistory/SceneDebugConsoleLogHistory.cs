using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        public List<SceneDebugConsoleLogEntry> FilteredLogMessages { get; private set; } = new ();
        private readonly List<SceneDebugConsoleLogEntry> unfilteredLogMessages = new();

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;
        private string textFilter;
        private bool filterOutErrorEntries = false;
        private bool filterOutLogEntries = false;

        public SceneDebugConsoleLogHistory() { }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            unfilteredLogMessages.Add(logEntry);

            if (string.IsNullOrEmpty(textFilter) || KeepAfterFilter(logEntry))
                FilteredLogMessages.Add(logEntry);

            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            unfilteredLogMessages.Clear();
            FilteredLogMessages.Clear();
        }

        public List<SceneDebugConsoleLogEntry> ApplyFilter(string targetText, bool filterOutErrors, bool filterOutLogs)
        {
            this.filterOutErrorEntries = filterOutErrors;
            this.filterOutLogEntries = filterOutLogs;
            textFilter = targetText;
            FilteredLogMessages.Clear();

            if (string.IsNullOrEmpty(targetText) && !filterOutErrorEntries && !filterOutLogEntries)
                FilteredLogMessages = new List<SceneDebugConsoleLogEntry>(unfilteredLogMessages);
            else
                FilteredLogMessages = unfilteredLogMessages.FindAll(KeepAfterFilter);

            return FilteredLogMessages;
        }

        private bool KeepAfterFilter(SceneDebugConsoleLogEntry entry) =>
            (string.IsNullOrEmpty(textFilter) || entry.Message.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
            && (!filterOutErrorEntries || entry.Type != LogMessageType.Error)
            && (!filterOutLogEntries || entry.Type != LogMessageType.Log);
    }
}
