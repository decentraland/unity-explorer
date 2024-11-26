using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Settings
{
    public class InWorldCameraTransitionSettings : ScriptableObject
    {
        [field: Header("MOVEMENT DAMPING")]
        [field: SerializeField] public float TranslationDamping { get; private set; } = 1f;
        [field: SerializeField] public float AimDamping { get; private set; } = 1f;

        [field: Header("BEHIND OFFSETS")]
        [field: SerializeField] public float BehindDirectionOffset { get; private set; } = 3f;
        [field: SerializeField] public float BehindUpOffset { get; private set; } = 0.5f;
    }
}
