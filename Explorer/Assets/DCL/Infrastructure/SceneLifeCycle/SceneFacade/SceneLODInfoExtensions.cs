using System.Collections.Generic;
using Arch.Core;
using DCL.LOD.Components;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace DCL.LOD
{
    public static class SceneLODInfoExtensions
    {
        public static void DisposeSceneLODAndReleaseToCache(this SceneLODInfo sceneLODInfo,
                                                                IScenesCache scenesCache,
                                                                IReadOnlyList<Vector2Int> parcels,
                                                                ILODCache lodCache,
                                                                World world,
                                                                float defaultFOV,
                                                                float defaultLodBias,
                                                                int loadingDistance,
                                                                int sceneParcels)
        {
            //Only try to release SceneLODInfo that has been initialized
            if (sceneLODInfo.IsInitialized())
            {
                sceneLODInfo.ClearISS(defaultFOV, defaultLodBias, loadingDistance, sceneParcels);
                lodCache.Release(sceneLODInfo.id, sceneLODInfo.metadata);
                sceneLODInfo.Dispose(world);
                scenesCache.RemoveNonRealScene(parcels);
            }
        }
    }
}
