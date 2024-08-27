using System.Collections.Generic;
using Arch.Core;
using DCL.LOD.Components;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace DCL.LOD
{
    public static class SceneLODInfoExtensions
    {
        public static void DisposeSceneLODAndRemoveFromCache(this SceneLODInfo sceneLODInfo,
                                                                IScenesCache scenesCache,
                                                                IReadOnlyList<Vector2Int> parcels,
            ILODCache lodCache,
                                                                World world)
        {
            //Only try to release SceneLODInfo that has been initialized
            if (sceneLODInfo.IsInitialized())
            {
                lodCache.Release(sceneLODInfo.id, sceneLODInfo.metadata);
                sceneLODInfo.Dispose(world);
                scenesCache.RemoveNonRealScene(parcels);
            }
        }
    }
}
