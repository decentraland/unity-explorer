using DCL.UI.EphemeralNotifications;
using UnityEngine;
using UnityEngine.Serialization;

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

        public AbstractEphemeralNotification DirectedSocialEmoteEphemeralNotificationPrefab;

        public AbstractEphemeralNotification DirectedEmoteEphemeralNotificationPrefab;
    }
}
