using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    [Unity.Burst.BurstCompile]
    public struct CachePoseJob : IWeightedAnimationJob
    {
        public ReadOnlyTransformHandle upperArm;
        public ReadOnlyTransformHandle forearm;
        public ReadOnlyTransformHandle hand;

        public NativeArray<Quaternion> cachedRotations;

        public FloatProperty jobWeight { get; set; }

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);

            if (w > 0f)
            {
                cachedRotations[0] = upperArm.GetLocalRotation(stream);
                cachedRotations[1] = forearm.GetLocalRotation(stream);
                cachedRotations[2] = hand.GetLocalRotation(stream);
            }
        }
    }
}
