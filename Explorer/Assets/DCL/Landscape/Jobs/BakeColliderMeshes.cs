using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct BakeColliderMeshes : IJobParallelFor
    {
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> Meshes;

        public void Execute(int index) =>
            Physics.BakeMesh(Meshes[index], false, MeshColliderCookingOptions.UseFastMidphase);
    }
}
