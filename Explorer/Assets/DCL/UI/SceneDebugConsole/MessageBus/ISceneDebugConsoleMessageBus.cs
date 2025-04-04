using System;
using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.MessageBus
{
    public interface ISceneDebugConsoleMessageBus
    {
        /// <summary>
        /// Event that is raised when a new log message is added to the message bus
        /// </summary>
        event Action<SceneDebugConsoleLogMessage> MessageAdded;

        /// <summary>
        /// Sends a message to the message bus
        /// </summary>
        /// <param name="message">The message content</param>
        /// <param name="logType">The type of log message</param>
        /// <param name="stackTrace">Optional stack trace for error messages</param>
        void Send(string message, LogType logType, string stackTrace = "");

        /// <summary>
        /// Sends a command message to the message bus
        /// </summary>
        /// <param name="command">The command text</param>
        void SendCommand(string command);

        /// <summary>
        /// Sends a command response message to the message bus
        /// </summary>
        /// <param name="response">The response text</param>
        void SendCommandResponse(string response);
    }
}
