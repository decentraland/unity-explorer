using System.Collections.Generic;
using UnityEngine;

// ORIGINAL OPEN SOURCE PLUGIN SCRIPT:
// https://github.com/stillwwater/command_terminal/blob/0f5918ea79014955b24c7d431625a97a1cac8797/CommandTerminal/CommandLog.cs

namespace CommandTerminal
{
    public struct LogItem
    {
        public LogType type;
        public string message;
        public string stackTrace;
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
            var log = new LogItem
            {
                message = message,
                stackTrace = stackTrace,
                type = logType,
            };

            Logs.Add(log);

            if (Logs.Count > maxItems) { Logs.RemoveAt(0); }
        }

        public void Clear()
        {
            Logs.Clear();
        }
    }
}
