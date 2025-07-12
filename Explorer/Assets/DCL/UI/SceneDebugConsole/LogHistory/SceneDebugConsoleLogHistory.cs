using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        public readonly List<SceneDebugConsoleLogEntry> LogMessages = new();
        private readonly int maxLogMessages;

        public event Action<SceneDebugConsoleLogEntry> LogMessageAdded;

        // TODO: Connect max log messages to existent setting
        public SceneDebugConsoleLogHistory(int maxLogMessages = 1000)
        {
            this.maxLogMessages = maxLogMessages;
        }

        public void AddLogMessage(SceneDebugConsoleLogEntry logEntry)
        {
            // Remove oldest entry if we've reached the limit
            // if (LogMessages.Count >= maxLogMessages)
            // {
            //     LogMessages.RemoveAt(0);
            // }

            LogMessages.Add(logEntry);
            LogMessageAdded?.Invoke(logEntry);
        }

        public void ClearLogMessages()
        {
            LogMessages.Clear();
        }
    }
}
