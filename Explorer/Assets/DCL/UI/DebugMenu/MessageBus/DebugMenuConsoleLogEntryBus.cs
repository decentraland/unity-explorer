using DCL.UI.DebugMenu.LogHistory;
using System;
using UnityEngine;

namespace DCL.UI.DebugMenu.MessageBus
{
    public class DebugMenuConsoleLogEntryBus
    {
        public event Action<DebugMenuConsoleLogEntry> MessageAdded;

        public void Send(string message, LogType logType)
        {
            DebugMenuConsoleLogEntry logEntry = DebugMenuConsoleLogEntry.FromUnityLog(logType, message);
            MessageAdded?.Invoke(logEntry);
        }
    }
}
