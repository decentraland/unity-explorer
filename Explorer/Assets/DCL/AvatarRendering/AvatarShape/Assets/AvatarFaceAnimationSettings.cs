using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Assets
{
    [CreateAssetMenu(fileName = "AvatarFaceAnimationSettings", menuName = "DCL/Avatar/Face Animation Settings")]
    public class AvatarFaceAnimationSettings : ScriptableObject
    {
        [field: Header("Blink")]
        [field: SerializeField] public float MinBlinkInterval { get; private set; } = 2.0f;
        [field: SerializeField] public float MaxBlinkInterval { get; private set; } = 8.0f;
        [field: SerializeField] public float BlinkFrameDuration { get; private set; } = 0.05f;

        [field: Header("Mouth Pose")]
        [field: SerializeField] public float MouthPoseDuration { get; private set; } = 0.08f;
        [field: SerializeField] public float VowelMouthPoseDuration { get; private set; } = 0.12f;
    }
}