using System;
using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.MessageBus
{
    public class SceneDebugConsoleLogEntryBus
    {
        public event Action<SceneDebugConsoleLogEntry> MessageAdded;

        public void Send(string message, LogType logType, string stackTrace = "")
        {
            SceneDebugConsoleLogEntry logEntry = SceneDebugConsoleLogEntry.FromUnityLog(logType, message, stackTrace);
            MessageAdded?.Invoke(logEntry);
        }

        public void SendCommand(string command)
        {
            SceneDebugConsoleLogEntry logEntry = SceneDebugConsoleLogEntry.Command(command);
            MessageAdded?.Invoke(logEntry);
        }

        public void SendCommandResponse(string response)
        {
            SceneDebugConsoleLogEntry logEntry = SceneDebugConsoleLogEntry.CommandResponse(response);
            MessageAdded?.Invoke(logEntry);
        }
    }
}
