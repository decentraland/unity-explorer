using UnityEngine;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(fileName = "ControlsSettings", menuName = "DCL/Settings/Controls Settings")]
    public class ControlsSettingsAsset : ScriptableObject
    {
        [Header("Mouse")]
        public float VerticalMouseSensitivity = 1;
        public float HorizontalMouseSensitivity = 1;
    }
}
