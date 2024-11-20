using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Settings
{
    public class InWorldCameraMovementSettings : ScriptableObject
    {
        [field: Header("TRANSLATION")]
        [field: SerializeField] public float MaxDistanceFromPlayer { get; private set; } = 16f;
        [field: SerializeField] public float TranslationSpeed { get; private set; } = 5f;
        [field: SerializeField] public float MouseTranslationSpeed { get; private set; } = 0.05f;
        [field: SerializeField] public float RunSpeedMultiplayer { get; private set; } = 2;

        [field: Header("FOV")]
        [field: SerializeField] public float FOVChangeSpeed { get; private set; } = 3;
        [field: SerializeField] public float FOVDamping { get; private set; } = 0.5f;
        [field: SerializeField] public float MinFOV { get; private set; } = 0;
        [field: SerializeField] public float MaxFOV { get; private set; } = 170;

        [field: Header("AIM")]
        [field: SerializeField] public float RotationSpeed { get; private set; } = 2;
        [field: SerializeField] public float MaxRotationPerFrame { get; private set; } = 10f;
        [field: SerializeField] public float RotationDamping { get; private set; } = 0.1f;
        [field: SerializeField] public float MinVerticalAngle { get; private set; } = -90f;
        [field: SerializeField] public float MaxVerticalAngle { get; private set; } = 90f;
    }
}
