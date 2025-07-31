using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        public List<SceneDebugConsoleLogEntry> FilteredLogMessages { get; private set; } = new ();
        private readonly List<SceneDebugConsoleLogEntry> unfilteredLogMessages = new();

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;
        public bool Paused = false;
        private string textFilter;
        private bool filterOutErrorEntries = false;
        private bool filterOutLogEntries = false;

        public SceneDebugConsoleLogHistory() { }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            if (Paused) return;

            unfilteredLogMessages.Add(logEntry);

            if (KeepAfterFilter(logEntry))
                FilteredLogMessages.Add(logEntry);

            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            unfilteredLogMessages.Clear();
            FilteredLogMessages.Clear();
        }

        // TODO: Optimize this, can we avoid relaying on the garbage collector so much?
        public List<SceneDebugConsoleLogEntry> ApplyFilter(string targetText, bool filterOutErrors, bool filterOutLogs)
        {
            filterOutErrorEntries = filterOutErrors;
            filterOutLogEntries = filterOutLogs;
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
