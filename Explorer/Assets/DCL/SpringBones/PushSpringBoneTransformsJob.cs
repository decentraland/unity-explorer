using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PushSpringBoneTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<quaternion> PrevRotations;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<quaternion> CurrRotations;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<bool> SlotActive;
        public int MaxJointsPerSpring;
        public float Alpha;

        public void Execute(int index, TransformAccess transform)
        {
            int slot = index / MaxJointsPerSpring;
            if (!SlotActive[slot]) return;

            transform.rotation = math.slerp(PrevRotations[index], CurrRotations[index], Alpha);
        }
    }
}
