using System;
using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.MessageBus
{
    public class SceneDebugConsoleLogEntryBus
    {
        public event Action<SceneDebugConsoleLogEntry> MessageAdded;

        public void Send(string message, LogType logType)
        {
            SceneDebugConsoleLogEntry logEntry = SceneDebugConsoleLogEntry.FromUnityLog(logType, message);
            MessageAdded?.Invoke(logEntry);
        }
    }
}
