using DCL.Character.CharacterCamera.Settings;
using UnityEngine;

namespace DCL.InWorldCamera.Settings
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

        [field: Header("POV")]
        [field: SerializeField] public CameraMovementPOVSettings PovSettings { get; private set; }

        [field: Header("TILT")]
        [field: SerializeField] public float TiltSpeed { get; private set; } = 30;
        [field: SerializeField] public int MaxTiltPerFrame { get; private set; } = 30;
        [field: SerializeField] public int MaxTiltAngle { get; private set; } = 360;
        [field: SerializeField] public float TiltDamping { get; private set; } = 0.1f;
    }
}
