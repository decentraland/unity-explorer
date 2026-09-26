using DCL.Optimization.Pools;
using Unity.Collections;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     These parcels were already processed and won't be processed again
    /// </summary>
    public struct ProcessedScenePointers
    {
        public NativeHashSet<int2> Value;

        public static ProcessedScenePointers Create() =>
            new () { Value = new NativeHashSet<int2>(PoolConstants.SCENES_COUNT * 4, AllocatorManager.Persistent) };
    }
}
