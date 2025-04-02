using System;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    [Serializable]
    public class SceneDebugConsoleSettings
    {
        [field: Header("Appearance")]
        [field: SerializeField] public Color LogColor { get; private set; } = Color.white;
        [field: SerializeField] public Color WarningColor { get; private set; } = Color.yellow;
        [field: SerializeField] public Color ErrorColor { get; private set; } = Color.red;
        [field: SerializeField] public Color CommandColor { get; private set; } = Color.cyan;
        [field: SerializeField] public Color CommandResponseColor { get; private set; } = Color.green;

        [field: Header("Behavior")]
        [field: SerializeField] public int MaxLogMessages { get; private set; } = 1000;
        [field: SerializeField] public bool ShowTimestamps { get; private set; } = true;
        [field: SerializeField] public bool AutoScrollToBottom { get; private set; } = true;
        [field: SerializeField] public bool CaptureUnityLogs { get; private set; } = true;
    }
}
