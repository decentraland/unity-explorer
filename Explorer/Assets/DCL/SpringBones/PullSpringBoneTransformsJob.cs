using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PullSpringBoneTransformsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<SpringBoneTransformData> Transforms;

        public void Execute(int index, TransformAccess transform)
        {
            Transforms[index] = SpringBoneTransformData.FromTransformAccess(transform);
        }
    }
}
