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

        public List<SceneDebugConsoleLogEntry> ApplyFilter(string targetText)
        {
            if (string.IsNullOrEmpty(targetText))
            {
                RemoveFilters();
                return FilteredLogMessages;
            }

            textFilter = targetText;
            FilteredLogMessages.Clear();
            FilteredLogMessages = unfilteredLogMessages.FindAll(KeepAfterFilter);

            return FilteredLogMessages;
        }

        private bool KeepAfterFilter(SceneDebugConsoleLogEntry entry) =>
            string.IsNullOrEmpty(textFilter) || entry.Message.Contains(textFilter, StringComparison.OrdinalIgnoreCase);

        public List<SceneDebugConsoleLogEntry> RemoveFilters()
        {
            if (string.IsNullOrEmpty(textFilter)) return null;

            textFilter = string.Empty;
            FilteredLogMessages.Clear();
            FilteredLogMessages = new List<SceneDebugConsoleLogEntry>(unfilteredLogMessages);

            return FilteredLogMessages;
        }
    }
}
