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

    public readonly struct SceneDebugConsoleLogEntry
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

        /// <summary>
        /// Color based on LogMessageType
        /// </summary>
        public Color Color { get; }

        public SceneDebugConsoleLogEntry(LogMessageType type, string message, string stackTrace = "")
        {
            Type = type;
            StackTrace = stackTrace;
            Timestamp = DateTime.Now;

            switch (type)
            {
                case LogMessageType.Warning:
                    Color = Color.yellow;
                    break;
                case LogMessageType.Error:
                    Color = Color.red;
                    break;
                case LogMessageType.Command:
                    Color = Color.cyan;
                    break;
                case LogMessageType.CommandResponse:
                    Color = Color.green;
                    break;
                default: // Log
                    Color = Color.white;
                    break;
            }

            Message = $"[{Timestamp:HH:mm:ss}] [{type}] {message}";
        }

        /// <summary>
        /// Creates a new log message from a Unity LogType
        /// </summary>
        public static SceneDebugConsoleLogEntry FromUnityLog(LogType logType, string message, string stackTrace = "")
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

            return new SceneDebugConsoleLogEntry(type, message, stackTrace);
        }

        public override string ToString()
        {
            string prefix = Type == LogMessageType.Error ? "[ERROR] " : "[LOG] ";
            return $"{prefix}{Message}";
        }
    }
}
