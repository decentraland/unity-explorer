using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    [Unity.Burst.BurstCompile]
    public struct AdditiveBreathJob : IWeightedAnimationJob
    {
        public ReadWriteTransformHandle upperArm;
        public ReadWriteTransformHandle forearm;
        public ReadWriteTransformHandle hand;

        [ReadOnly] public NativeArray<Quaternion> cachedRotations;
        [ReadOnly] public NativeArray<Quaternion> bindPoseRotations;

        public FloatProperty jobWeight { get; set; }

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);

            if (w > 0f)
            {
                ApplyAdditive(stream, ref upperArm, 0, w);
                ApplyAdditive(stream, ref forearm, 1, w);
                ApplyAdditive(stream, ref hand, 2, w);
            }
            else
            {
                AnimationRuntimeUtils.PassThrough(stream, upperArm);
                AnimationRuntimeUtils.PassThrough(stream, forearm);
                AnimationRuntimeUtils.PassThrough(stream, hand);
            }
        }

        private void ApplyAdditive(AnimationStream stream, ref ReadWriteTransformHandle bone, int index, float weight)
        {
            Quaternion animated = cachedRotations[index];
            Quaternion bindPose = bindPoseRotations[index];

            // Delta = how the animated pose differs from the static bind pose (i.e., the breathing offset)
            Quaternion delta = animated * Quaternion.Inverse(bindPose);

            // Current post-IK rotation
            Quaternion postIK = bone.GetLocalRotation(stream);

            // Apply the breathing delta additively, scaled by weight
            Quaternion scaledDelta = Quaternion.SlerpUnclamped(Quaternion.identity, delta, weight);
            bone.SetLocalRotation(stream, postIK * scaledDelta);
        }
    }
}
