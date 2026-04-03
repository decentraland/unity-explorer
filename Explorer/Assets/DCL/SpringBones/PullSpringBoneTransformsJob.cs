using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PullSpringBoneTransformsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<BlittableTransform> Transforms;

        [BurstCompile]
        public void Execute(int index, TransformAccess transform) =>
            Transforms[index] = BlittableTransform.FromTransformAccess(transform);
    }
}
