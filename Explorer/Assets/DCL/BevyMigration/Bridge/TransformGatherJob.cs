using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Gathers local-to-world matrices for every registered renderer in parallel (BevyMigration/docs/README.md
    ///     § 8: "a Burst job over a TransformAccessArray, never a managed loop over localToWorldMatrix").
    /// </summary>
    [BurstCompile]
    public struct TransformGatherJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<float4x4> Worlds;

        public void Execute(int index, TransformAccess transform)
        {
            Worlds[index] = transform.localToWorldMatrix;
        }
    }
}
