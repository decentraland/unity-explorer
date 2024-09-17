
using DCL.CharacterMotion.Components;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    [DisplayName("Avatar teleport clip")]
    public class TeleportAvatarPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The position and rotation the avatar will copy.")]
        public ExposedReference<Transform> ReferenceTransform;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<TeleportAvatarPlayableBehaviour> playable = ScriptPlayable<TeleportAvatarPlayableBehaviour>.Create(graph);

            TeleportAvatarPlayableBehaviour behaviour = playable.GetBehaviour();
            behaviour.ReferenceTransform = ReferenceTransform.Resolve(graph.GetResolver());

            return playable;
        }

        public ClipCaps clipCaps => ClipCaps.Looping;
    }
}
