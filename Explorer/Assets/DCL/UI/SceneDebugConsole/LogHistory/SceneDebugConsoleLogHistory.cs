using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        public readonly List<SceneDebugConsoleLogEntry> UnfilteredLogMessages = new();
        public List<SceneDebugConsoleLogEntry> FilteredLogMessages { get; private set; } = new ();

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;
        private string textFilter;

        public SceneDebugConsoleLogHistory() { }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            UnfilteredLogMessages.Add(logEntry);

            if (string.IsNullOrEmpty(textFilter) || logEntry.Message.Contains(textFilter))
                FilteredLogMessages.Add(logEntry);

            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            UnfilteredLogMessages.Clear();
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
            FilteredLogMessages = UnfilteredLogMessages.FindAll(entry => entry.Message.Contains(textFilter, StringComparison.OrdinalIgnoreCase));

            return FilteredLogMessages;
        }

        public List<SceneDebugConsoleLogEntry> RemoveFilters()
        {
            if (string.IsNullOrEmpty(textFilter)) return null;

            textFilter = string.Empty;
            FilteredLogMessages.Clear();
            FilteredLogMessages = new List<SceneDebugConsoleLogEntry>(UnfilteredLogMessages);

            return FilteredLogMessages;
        }
    }
}
