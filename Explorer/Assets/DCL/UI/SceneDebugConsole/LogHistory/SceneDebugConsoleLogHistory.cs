using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public class SceneDebugConsoleLogHistory
    {
        private readonly List<SceneDebugConsoleLogMessage> logMessages = new();
        private readonly int maxLogMessages;

        public event Action<SceneDebugConsoleLogMessage> LogMessageAdded;

        public IReadOnlyList<SceneDebugConsoleLogMessage> LogMessages => logMessages;

        // TODO: Connect max log messages to existent setting
        public SceneDebugConsoleLogHistory(int maxLogMessages = 1000)
        {
            this.maxLogMessages = maxLogMessages;
        }

        public void AddLogMessage(SceneDebugConsoleLogMessage logMessage)
        {
            // Remove oldest message if we've reached the limit
            if (logMessages.Count >= maxLogMessages)
            {
                logMessages.RemoveAt(0);
            }

            logMessages.Add(logMessage);
            LogMessageAdded?.Invoke(logMessage);
        }

        public void ClearLogMessages()
        {
            logMessages.Clear();
        }
    }
}
