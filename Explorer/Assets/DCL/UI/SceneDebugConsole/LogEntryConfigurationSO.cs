using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    [CreateAssetMenu(fileName = "LogEntryConfiguration", menuName = "DCL/SceneDebugConsole/Log Entry Configuration")]
    public class LogEntryConfigurationSO : ScriptableObject
    {
        [field: SerializeField] public float BackgroundHeightOffset { private set; get; } = 56;
        [field: SerializeField] public float BackgroundWidthOffset { private set; get; } = 56;
        [field: SerializeField] public float MaxEntryWidth { private set; get; } = 246;
    }
}
