using System;
using UnityEngine;

namespace DCL.CharacterPreview
{
    [CreateAssetMenu(fileName = "CharacterPreviewSettings", menuName = "SO/CharacterPreviewSettings")]
    public class CharacterPreviewSettingsSO : ScriptableObject
    {
        [field: Header("Camera Settings")]
        [field: SerializeField] public CharacterPreviewCameraSettings cameraSettings { get; private set; }

        [field: Header("Cursor Settings")]
        [field: SerializeField] public CharacterPreviewInputCursorSetting[] cursorSettings { get; private set; }
    }

    [Serializable]
    public struct CharacterPreviewCameraPreset
    {
        [field: SerializeField] internal Vector3 verticalPosition { get; private set; }
        [field: SerializeField] internal float cameraFieldOfView { get; private set; }
        [field: SerializeField] internal AvatarWearableCategoryEnum wearableCategoryEnum { get; private set; }
    }

    [Serializable]
    public struct CharacterPreviewCameraSettings
    {
        [field: Header("Camera Settings")]
        [field: SerializeField] internal CharacterPreviewCameraPreset[] cameraPositions { get; private set; }

        [field: Header("Drag Settings")]
        [field: SerializeField] public bool dragEnabled { get; private set; }
        [field: SerializeField] internal float dragMovementModifier { get; private set; }
        [field: SerializeField] internal float minVerticalOffset { get; private set; }
        [field: SerializeField] internal float maxVerticalOffset { get; private set; }

        [field: Header("Scroll Settings")]
        [field: SerializeField] public bool scrollEnabled { get; private set; }
        [field: SerializeField] internal float scrollModifier { get; private set; }
        [field: SerializeField] internal float fieldOfViewThresholdForPanning { get; private set; }
        [field: SerializeField] internal float fieldOfViewThresholdForReCentering { get; private set; }
        [field: SerializeField] internal Vector2 fieldOfViewLimits { get; private set; }

        [field: Header("Rotation Settings")]
        [field: SerializeField] public bool rotationEnabled { get; private set; }
        [field: SerializeField] public float rotationModifier { get; private set; }
    }

    [Serializable]
    public struct CharacterPreviewInputCursorSetting
    {
        [field: SerializeField] internal CharacterPreviewInputAction inputAction;
        [field: SerializeField] internal Sprite cursorSprite;
    }

    public enum CharacterPreviewInputAction
    {
        VerticalPan,
        Rotate,
    }
}
