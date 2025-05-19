using DCL.Character.CharacterCamera.Settings;
using UnityEngine;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(fileName = "ControlsSettings", menuName = "DCL/Settings/Controls Settings")]
    public class ControlsSettingsAsset : ScriptableObject
    {
        [Header("Mouse")]
        public float VerticalMouseSensitivity = 1;
        public float HorizontalMouseSensitivity = 1;

        [Header("3d Person Camera")]
        public CameraMovementPOVSettings CameraMovementPOVSettings;
        public CameraMovementPOVSettings DroneCameraMovementPOVSettings;
    }

}
