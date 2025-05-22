using UnityEngine;

namespace DCL.Character.CharacterCamera.Settings
{
    [CreateAssetMenu(fileName = "CameraMovementPOVSettings", menuName = "DCL/Camera/CameraMovementPOVSettings")]
    public class CameraMovementPOVSettings : ScriptableObject
    {
        [field: SerializeField] public float RotationSpeed { get; private set; } = 2;
        [field: SerializeField] public float MaxRotationPerFrame { get; private set; } = 10f;
        [field: SerializeField] public float RotationDamping { get; private set; } = 0.1f;
        [field: SerializeField] public float MinVerticalAngle { get; private set; } = -90f;
        [field: SerializeField] public float MaxVerticalAngle { get; private set; } = 90f;
    }
}
