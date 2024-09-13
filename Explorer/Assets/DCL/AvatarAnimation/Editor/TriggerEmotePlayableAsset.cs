
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    public class TriggerEmotePlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        public string URN;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<TriggerEmotePlayableBehaviour> playable = ScriptPlayable<TriggerEmotePlayableBehaviour>.Create(graph);

            TriggerEmotePlayableBehaviour behaviour = playable.GetBehaviour();

            behaviour.URN = URN;

            return playable;
        }

        public ClipCaps clipCaps => ClipCaps.Looping;
    }
}
