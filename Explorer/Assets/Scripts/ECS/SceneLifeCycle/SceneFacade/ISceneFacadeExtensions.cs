using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace SceneRunner.Scene
{
    public static class ISceneFacadeExtensions
    {
        public static void DisposeSceneFacadeAndRemoveFromCache(this ISceneFacade sceneFacade, IScenesCache scenesCache,
            IReadOnlyList<Vector2Int> parcels, SceneAssetLock assetLock)
        {
            assetLock.TryUnlock(sceneFacade);

            sceneFacade.DisposeAsync().Forget();
            scenesCache.RemoveSceneFacade(parcels);
        }
    }
}
