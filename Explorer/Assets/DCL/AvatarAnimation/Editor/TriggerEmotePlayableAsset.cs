
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    [DisplayName("Emote triggering clip")]
    public class TriggerEmotePlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The full URN of the emote to play, either remote (starts with 'urn:') or local (like 'wave').")]
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
