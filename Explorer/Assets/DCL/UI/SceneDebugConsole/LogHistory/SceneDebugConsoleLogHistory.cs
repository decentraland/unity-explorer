using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        public readonly List<SceneDebugConsoleLogEntry> LogMessages = new();
        public List<SceneDebugConsoleLogEntry> FilteredLogMessages { get; private set; } = new ();

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;


        public SceneDebugConsoleLogHistory() { }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            LogMessages.Add(logEntry);
            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            LogMessages.Clear();
        }

        public List<SceneDebugConsoleLogEntry> ApplyFilter(string targetText)
        {
            FilteredLogMessages.Clear();
            FilteredLogMessages = LogMessages.FindAll(entry => entry.Message.Contains(targetText, StringComparison.OrdinalIgnoreCase));
            // Debug.Log($"PRAVS - new filtered count for '{targetText}': { FilteredLogMessages.Count }");
            return FilteredLogMessages;
        }

        // public IReadOnlyList<SceneDebugConsoleLogEntry> ApplyLogTypeFilter(LogMessageType targetLogtype)
        // {
        //
        // }
    }
}
