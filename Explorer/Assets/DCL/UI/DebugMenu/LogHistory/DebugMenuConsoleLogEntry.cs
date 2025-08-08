using System;
using UnityEngine;

namespace DCL.UI.DebugMenu.LogHistory
{
    public enum LogMessageType
    {
        Log,
        Warning,
        Error,
    }

    public readonly struct DebugMenuConsoleLogEntry
    {
        private const string ERROR_ENTRY_PREFIX = "[ERROR] ";
        private const string LOG_ENTRY_PREFIX = "[LOG] ";

        public LogMessageType Type { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public DebugMenuConsoleLogEntry(LogMessageType type, string message)
        {
            Type = type;
            Timestamp = DateTime.Now;
            Message = $"[{Timestamp:HH:mm:ss}] [{type}] {message}";
        }

        /// <summary>
        /// Creates a new log message from a Unity LogType
        /// </summary>
        public static DebugMenuConsoleLogEntry FromUnityLog(LogType logType, string message)
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

            return new DebugMenuConsoleLogEntry(type, message);
        }

        public override string ToString()
        {
            string prefix = Type == LogMessageType.Error ? ERROR_ENTRY_PREFIX : LOG_ENTRY_PREFIX;
            return $"{prefix}{Message}";
        }
    }
}
