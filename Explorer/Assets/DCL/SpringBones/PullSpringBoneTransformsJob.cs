using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PullSpringBoneTransformsJob : IJobParallelForTransform
    {
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<SpringBoneTransformData> Transforms;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<bool> SlotActive;
        public int MaxJointsPerSpring;

        public void Execute(int index, TransformAccess transform)
        {
            int slot = index / MaxJointsPerSpring;
            if (!SlotActive[slot]) return;
            Transforms[index] = SpringBoneTransformData.FromTransformAccess(transform);
        }
    }
}
