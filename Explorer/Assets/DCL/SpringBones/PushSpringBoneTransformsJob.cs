using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PushSpringBoneTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<SpringBoneTransformData> Transforms;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<bool> SlotActive;
        public int MaxJointsPerSpring;

        public void Execute(int index, TransformAccess transform)
        {
            int slot = index / MaxJointsPerSpring;
            if (!SlotActive[slot]) return;
            transform.rotation = Transforms[index].Rotation;
        }
    }
}
