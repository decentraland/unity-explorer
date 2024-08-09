using System.Collections.Generic;
using UnityEngine;

// ORIGINAL OPEN SOURCE PLUGIN SCRIPT:
// https://github.com/stillwwater/command_terminal/blob/0f5918ea79014955b24c7d431625a97a1cac8797/CommandTerminal/CommandLog.cs

namespace CommandTerminal
{
    public readonly struct LogItem
    {
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;

        public LogItem(LogType type, string message, string stackTrace)
        {
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    public class ConsoleBuffer
    {
        private readonly int maxItems;
        private List<LogItem> logs { get; } = new ();

        public IReadOnlyList<LogItem> Logs => logs;

        public ConsoleBuffer(int maxItems)
        {
            this.maxItems = maxItems;
        }

        public void HandleLog(string message, string stackTrace, LogType logType)
        {
            logs.Add(new LogItem(logType, message, stackTrace));

            // Logs will only be added from the local SDK Scene (no other scenes consuming performance)
            // If we detect this check becomes a problem we may change it
            if (logs.Count > maxItems) { logs.RemoveAt(0); }
        }

        public void Clear()
        {
            logs.Clear();
        }
    }
}
