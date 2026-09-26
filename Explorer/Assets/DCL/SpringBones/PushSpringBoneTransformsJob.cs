using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PushSpringBoneTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<quaternion> PrevRotations;
        [ReadOnly] public NativeArray<quaternion> CurrRotations;
        [ReadOnly] public NativeArray<bool> SlotActive;
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
