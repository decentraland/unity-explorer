using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    public class CachePoseBinder : AnimationJobBinder<CachePoseJob, CachePoseData>
    {
        public override CachePoseJob Create(Animator animator, ref CachePoseData data, Component component)
        {
            var job = new CachePoseJob
            {
                upperArm = ReadOnlyTransformHandle.Bind(animator, data.UpperArm),
                forearm = ReadOnlyTransformHandle.Bind(animator, data.Forearm),
                hand = ReadOnlyTransformHandle.Bind(animator, data.Hand),
                cachedRotations = data.Bridge.CachedRotations,
            };

            return job;
        }

        public override void Destroy(CachePoseJob job) { }
    }
}
