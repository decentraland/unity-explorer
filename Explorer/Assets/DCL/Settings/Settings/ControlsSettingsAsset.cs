using UnityEngine;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(menuName = "Create Controls Settings", fileName = "ControlsSettings", order = 0)]
    public class ControlsSettingsAsset : ScriptableObject
    {
        [Header("Mouse")]
        public float VerticalMouseSensitivity = 1;
        public float HorizontalMouseSensitivity = 1;
    }
}
