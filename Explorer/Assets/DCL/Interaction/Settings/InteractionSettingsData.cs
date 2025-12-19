using UnityEngine;

namespace DCL.Interaction.Settings
{
    /// <summary>
    /// Parameters that control how the objects and avatars highlight FX behave.
    /// </summary>
    public class InteractionSettingsData : ScriptableObject
    {
        [Header("Scene objects")]

        [field: SerializeField] public Color ValidColor { get; private set; }
        [field: SerializeField] public Color InvalidColor { get; private set; }
        [field: SerializeField] public float Thickness { get; private set; }

        [Header("Avatar")]

        /// <summary>
        /// When enabled, avatar outline VFX will be computed and visible.
        /// </summary>
        [Tooltip("When enabled, avatar outline VFX will be computed and visible.")]
        public bool EnableAvatarOutline = true;

        /// <summary>
        /// The color of the avatar outline when the user is able to interact.
        /// </summary>
        [Tooltip("The color of the avatar outline when the user is able to interact")]
        public Color InteractableAvatarOutlineColor = Color.green;

        /// <summary>
        /// The color of the avatar outline when the user is not able to interact.
        /// </summary>
        [Tooltip("The color of the avatar outline when the user is not able to interact")]
        public Color NonInteractableAvatarOutlineColor = Color.red;

        /// <summary>
        /// The thickness of the avatar outline.
        /// </summary>
        [Tooltip("The thickness of the avatar outline.")]
        public float AvatarOutlineThickness = 0.1f;

        /// <summary>
        /// How fast the outline gets totally opaque or transparent (1 second / value).
        /// </summary>
        [Tooltip("How fast the outline gets totally opaque or transparent (1 second / value).")]
        public float AvatarOutlineFadingSpeed = 12.0f;
    }
}
