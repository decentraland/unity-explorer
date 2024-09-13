
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    public class MoveAvatarPlayableAsset : PlayableAsset
    {
        public ExposedReference<Transform> Point;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<MoveAvatarPlayableBehaviour> playable = ScriptPlayable<MoveAvatarPlayableBehaviour>.Create(graph);

            MoveAvatarPlayableBehaviour behaviour = playable.GetBehaviour();
            behaviour.Point = Point.Resolve(graph.GetResolver());

            return playable;
        }
    }
}
