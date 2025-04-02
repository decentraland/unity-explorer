using System;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public enum LogMessageType
    {
        Log,
        Warning,
        Error,
        Command,
        CommandResponse
    }

    public class SceneDebugConsoleLogMessage
    {
        /// <summary>
        /// The type of message (log, warning, error, etc.)
        /// </summary>
        public LogMessageType Type { get; }

        /// <summary>
        /// The actual message content
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Optional stack trace for error messages
        /// </summary>
        public string StackTrace { get; }

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; }

        public SceneDebugConsoleLogMessage(LogMessageType type, string message, string stackTrace = "")
        {
            Type = type;
            Message = message;
            StackTrace = stackTrace;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Creates a new log message from a Unity LogType
        /// </summary>
        public static SceneDebugConsoleLogMessage FromUnityLog(LogType logType, string message, string stackTrace = "")
        {
            LogMessageType type = LogMessageType.Log;

            switch (logType)
            {
                case LogType.Log:
                    type = LogMessageType.Log;
                    break;
                case LogType.Warning:
                    type = LogMessageType.Warning;
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    type = LogMessageType.Error;
                    break;
            }

            return new SceneDebugConsoleLogMessage(type, message, stackTrace);
        }

        /// <summary>
        /// Creates a new command message
        /// </summary>
        public static SceneDebugConsoleLogMessage Command(string commandText)
        {
            return new SceneDebugConsoleLogMessage(LogMessageType.Command, commandText);
        }

        /// <summary>
        /// Creates a new command response message
        /// </summary>
        public static SceneDebugConsoleLogMessage CommandResponse(string responseText)
        {
            return new SceneDebugConsoleLogMessage(LogMessageType.CommandResponse, responseText);
        }
    }
}
