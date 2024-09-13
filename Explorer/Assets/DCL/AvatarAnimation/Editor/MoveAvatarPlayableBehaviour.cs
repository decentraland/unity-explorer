
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine;

namespace DCL.AvatarAnimation
{
    public class MoveAvatarPlayableBehaviour : PlayableBehaviour
    {
        public Transform Point;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Ensure move is executed only once

            AvatarBase avatar = (AvatarBase)playerData;

            // Find the corresponding entity in the global world
            // Move root object position directly or via ECS
        }
    }
}
