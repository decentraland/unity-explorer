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

        public List<LogItem> Logs { get; } = new ();

        public ConsoleBuffer(int maxItems)
        {
            this.maxItems = maxItems;
        }

        public void HandleLog(string message, string stackTrace, LogType logType)
        {
            Logs.Add(new LogItem(logType, message, stackTrace));
            if (Logs.Count > maxItems) { Logs.RemoveAt(0); }
        }

        public void Clear()
        {
            Logs.Clear();
        }
    }
}
