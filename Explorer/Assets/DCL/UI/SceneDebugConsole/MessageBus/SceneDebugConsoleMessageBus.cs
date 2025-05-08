using System;
using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.MessageBus
{
    public class SceneDebugConsoleMessageBus
    {
        public event Action<SceneDebugConsoleLogMessage> MessageAdded;

        public void Send(string message, LogType logType, string stackTrace = "")
        {
            SceneDebugConsoleLogMessage logMessage = SceneDebugConsoleLogMessage.FromUnityLog(logType, message, stackTrace);
            MessageAdded?.Invoke(logMessage);
        }

        public void SendCommand(string command)
        {
            SceneDebugConsoleLogMessage logMessage = SceneDebugConsoleLogMessage.Command(command);
            MessageAdded?.Invoke(logMessage);
        }

        public void SendCommandResponse(string response)
        {
            SceneDebugConsoleLogMessage logMessage = SceneDebugConsoleLogMessage.CommandResponse(response);
            MessageAdded?.Invoke(logMessage);
        }
    }
}
