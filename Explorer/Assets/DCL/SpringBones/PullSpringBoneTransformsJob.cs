using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PullSpringBoneTransformsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<SpringBoneTransformData> Transforms;
        [ReadOnly] public NativeArray<bool> SlotActive;
        public int MaxJointsPerSpring;

        public void Execute(int index, TransformAccess transform)
        {
            int slot = index / MaxJointsPerSpring;
            if (!SlotActive[slot]) return;
            Transforms[index] = SpringBoneTransformData.FromTransformAccess(transform);
        }
    }
}
