using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    [CreateAssetMenu(fileName = "SceneDebugConsoleSettings", menuName = "DCL/SceneDebugConsole/Plugin Settings")]
    public class SceneDebugConsoleSettings : ScriptableObject
    {
        [field: Header("Behavior")]
        [field: SerializeField] public int MaxLogMessages { get; private set; } = 1000;
        [field: SerializeField] public bool ShowTimestamps { get; private set; } = true;
        [field: SerializeField] public bool AutoScrollToBottom { get; private set; } = true;
        [field: SerializeField] public bool CaptureUnityLogs { get; private set; } = true;
    }
}
