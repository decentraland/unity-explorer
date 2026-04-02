using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    [BurstCompile]
    public struct PushSpringBoneTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<SpringBoneTransformData> Transforms;

        public void Execute(int index, TransformAccess transform)
        {
            transform.rotation = Transforms[index].Rotation;
        }
    }
}
