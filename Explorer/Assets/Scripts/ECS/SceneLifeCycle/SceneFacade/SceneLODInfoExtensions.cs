using System.Collections.Generic;
using Arch.Core;
using DCL.LOD.Components;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace DCL.LOD
{
    public static class SceneLODInfoExtensions
    {
        public static void DisposeSceneLODAndRemoveFromCache(this SceneLODInfo sceneLODInfo, IScenesCache scenesCache,
            IReadOnlyList<Vector2Int> parcels, World world)
        {
            sceneLODInfo.Dispose(world);
            scenesCache.RemoveNonRealScene(parcels);
        }
    }
}