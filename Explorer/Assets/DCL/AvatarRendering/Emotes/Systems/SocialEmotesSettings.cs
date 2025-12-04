using DCL.UI.EphemeralNotifications;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Parameters that control how users interact with each other through social emotes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSocialEmotesSettings", menuName = "DCL/Settings/SocialEmotesSettings")]
    public class SocialEmotesSettings : ScriptableObject
    {
        /// <summary>
        /// When enabled, the receiver will jog towards the initiator of the social emote. Otherwise, it will walk.
        /// </summary>
        [Tooltip("When enabled, the receiver will jog towards the initiator of the social emote. Otherwise, it will walk.")]
        public bool ReceiverJogs;

        /// <summary>
        /// The minimum distance between the initiator and the receiver for the interaction tooltips to appear in receiver's client.
        /// </summary>
        [Tooltip("The minimum distance between the initiator and the receiver for the interaction tooltips to appear in receiver's client.")]
        public float VisibilityDistance;

        /// <summary>
        /// The minimum distance between the initiator and the receiver for receiver to be able to react to the social emote.
        /// </summary>
        [Tooltip("The minimum distance between the initiator and the receiver for receiver to be able to react to the social emote.")]
        public float InteractionDistance;

        /// <summary>
        /// The minimum distance between the initiator and the receiver for receiver to interpolate its position / rotation to the first pose of the outcome animation.
        /// </summary>
        [Tooltip("The minimum distance between the initiator and the receiver for receiver to interpolate its position / rotation to the first pose of the outcome animation.")]
        public float OutcomeStartInterpolationRadius;

        /// <summary>
        /// The duration of the initial interpolation of the pose of the receiver, in seconds.
        /// </summary>
        [Tooltip("The duration, in seconds, of the initial interpolation of the pose of the receiver.")]
        public float OutcomeStartInterpolationDuration;

        /// <summary>
        /// The duration of the interpolation of the camera when initiating or finishing the reaction to a social emote, in seconds.
        /// </summary>
        [Tooltip("The duration of the interpolation of the camera when initiating or finishing the reaction to a social emote, in seconds.")]
        public float OutcomeCameraInterpolationDuration;

        [Header("Avatar Outline")]

        /// <summary>
        /// The duration of the animation of the initiator's avatar outline that makes the outline blink when the receiver receives a directed social emote.
        /// </summary>
        [Tooltip("The duration of the animation of the initiator's avatar outline that makes the outline blink when the receiver receives a directed social emote.")]
        public float DirectedEmoteReceivedAnimationDuration = 1.0f;

        /// <summary>
        /// The amount of times the initiator's avatar outline blinks when the receiver receives a directed social emote.
        /// </summary>
        [Tooltip("The amount of times the initiator's avatar outline blinks when the receiver receives a directed social emote.")]
        public int DirectedEmoteReceivedAnimationLoops = 2;

        /// <summary>
        /// The color of the initiator's avatar outline when the receiver receives a directed social emote.
        /// </summary>
        [Tooltip("The color of the initiator's avatar outline when the receiver receives a directed social emote.")]
        public Color DirectedEmoteReceivedAnimationColor = Color.cyan;

        /// <summary>
        /// The thickness of the avatar outline when the receiver receives a directed social emote.
        /// </summary>
        [Tooltip("The thickness of the avatar outline.")]
        public float DirectedEmoteReceivedAnimationThickness = 2.0f;

        [Header("Ephemeral notification prefabs")]

        public AbstractEphemeralNotification DirectedSocialEmoteEphemeralNotificationPrefab;

        public AbstractEphemeralNotification DirectedEmoteEphemeralNotificationPrefab;
    }
}
