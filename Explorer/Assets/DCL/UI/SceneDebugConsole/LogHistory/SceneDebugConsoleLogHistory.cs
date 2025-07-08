using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        private readonly List<SceneDebugConsoleLogEntry> logMessages = new();
        private readonly int maxLogMessages;

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;

        public IReadOnlyList<SceneDebugConsoleLogEntry> LogMessages => logMessages;

        // TODO: Connect max log messages to existent setting
        public SceneDebugConsoleLogHistory(int maxLogMessages = 1000)
        {
            this.maxLogMessages = maxLogMessages;
        }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            // Remove oldest entry if we've reached the limit
            if (logMessages.Count >= maxLogMessages)
            {
                logMessages.RemoveAt(0);
            }

            logMessages.Add(logEntry);
            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            logMessages.Clear();
        }
    }
}
