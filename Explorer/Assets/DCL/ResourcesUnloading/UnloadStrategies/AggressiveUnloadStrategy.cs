using System;
using Cysharp.Threading.Tasks;
using ECS.Prioritization;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        public bool isRunning { get; set; }

        private readonly IRealmPartitionSettings realmPartitionSettings;
        private const int FORCE_UNLOAD_DURING_THIS_FRAMES = 200;

        public AggressiveUnloadStrategy(IRealmPartitionSettings realmPartitionSettings)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            if (isRunning)
                return;
            
            isRunning = true;
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            Debug.Log("JUANI RUNNING AGGRESIVE");
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;
            StartAggressiveUnload(cacheCleaner).Forget();
        }

        private async UniTaskVoid StartAggressiveUnload(ICacheCleaner cacheCleaner)
        {
            var currentFrameRunning = 0;
            try
            {
                while (currentFrameRunning < FORCE_UNLOAD_DURING_THIS_FRAMES)
                {
                    cacheCleaner.UnloadCache();
                    cacheCleaner.UpdateProfilingCounters();
                    currentFrameRunning++;
                    await UniTask.Yield();
                }
            }
            catch (Exception e)
            {
                // ignored
            }
            finally
            {
                isRunning = false;
                //Finally, we unload assets that are unreferenced and not referenced
                Resources.UnloadUnusedAssets();
            }
        }

    }
}