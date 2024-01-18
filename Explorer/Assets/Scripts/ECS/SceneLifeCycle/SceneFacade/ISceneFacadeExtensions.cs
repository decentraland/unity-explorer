using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace SceneRunner.Scene
{
    public static class ISceneFacadeExtensions
    {
        public static void DisposeSceneFacadeAndRemoveFromCache(this ISceneFacade sceneFacade, IScenesCache scenesCache,
            IReadOnlyList<Vector2Int> parcels)
        {
            //TODO: Misha, DisposeAsync is callen in many places. Should all of them add the parcels back to the cache?
            sceneFacade.DisposeAsync().Forget();
            scenesCache.Remove(parcels);
        }
    }
}