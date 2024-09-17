
using DCL.CharacterMotion.Components;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    [DisplayName("Avatar movement clip")]
    public class MoveAvatarPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("Makes the avatar move forward with a given velocity (depends on the movement animation). While the clip is not playing, velocity equals zero.")]
        [Range(0.0f, 1.0f)]
        public float Forward = 0.0f;

        [Tooltip("Makes the avatar use a given animation while moving forward. While the clip is not playing, the animation is Idle.")]
        public MovementKind MovementAnimation = MovementKind.IDLE;

        [Tooltip("Makes the avatar rotate a given amount of degrees per second. Positive means turning to the right.")]
        [Range(-360.0f, 360.0f)]
        public float Rotation = 0.0f;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<MoveAvatarPlayableBehaviour> playable = ScriptPlayable<MoveAvatarPlayableBehaviour>.Create(graph);

            MoveAvatarPlayableBehaviour behaviour = playable.GetBehaviour();
            behaviour.Forward = Forward;
            behaviour.MovementAnimation = MovementAnimation;
            behaviour.Rotation = Rotation;

            return playable;
        }

        public ClipCaps clipCaps => ClipCaps.Looping;
    }
}
