using DCL.UI.EphemeralNotifications;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///
    /// </summary>
    [CreateAssetMenu(fileName = "NewSocialEmotesSettings", menuName = "DCL/Settings/SocialEmotesSettings")]
    public class SocialEmotesSettings : ScriptableObject
    {
        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public bool ReceiverJogs;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float VisibilityDistance;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float InteractionDistance;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float OutcomeStartInterpolationRadius;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float OutcomeStartInterpolationDuration;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float OutcomeCameraInterpolationDuration;

        [Header("Avatar Outline")]

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public bool EnabledOutline = true;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public Color InteractableAvatarOutlineColor = Color.green;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public Color NonInteractableAvatarOutlineColor = Color.red;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float DirectedEmoteReceivedAnimationDuration = 1.0f;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public int DirectedEmoteReceivedAnimationLoops = 2;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public Color DirectedEmoteReceivedAnimationColor = Color.cyan;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float AvatarOutlineThickness = 0.1f;

        /// <summary>
        ///
        /// </summary>
        [Tooltip("")]
        public float AvatarOutlineFadingSpeed = 12.0f;

        [Header("Ephemeral notification prefabs")]

        public AbstractEphemeralNotification DirectedSocialEmoteEphemeralNotificationPrefab;

        public AbstractEphemeralNotification DirectedEmoteEphemeralNotificationPrefab;
    }
}
