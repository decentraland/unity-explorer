using System;
using Cysharp.Threading.Tasks;
using ECS.Prioritization;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        public bool IsRunning { get; set; }

        private readonly IRealmPartitionSettings realmPartitionSettings;
        private const int FORCE_UNLOADING_FRAMES_AMOUNT = 200;

        public AggressiveUnloadStrategy(IRealmPartitionSettings realmPartitionSettings)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            IsRunning = true;
            
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;
            
            StartAggressiveUnloadAsync(cacheCleaner).Forget();
        }

        private async UniTaskVoid StartAggressiveUnloadAsync(ICacheCleaner cacheCleaner)
        {
            var currentFrameRunning = 0;
            try
            {
                while (currentFrameRunning < FORCE_UNLOADING_FRAMES_AMOUNT)
                {
                    cacheCleaner.UnloadCache();
                    cacheCleaner.UpdateProfilingCounters();
                    currentFrameRunning++;
                    await UniTask.Yield();
                }
            }
            catch (Exception e)
            {
                // On any exception, lets keep running the unloading process until the end
                while (currentFrameRunning < FORCE_UNLOADING_FRAMES_AMOUNT)
                {
                    currentFrameRunning++;
                    await UniTask.Yield();
                }
            }
            finally
            {
                IsRunning = false;
                //Finally, we unload assets that are unreferenced by no one
                //Its a safe-check for any asset we may have missed in the cache
                //Careful, because it will produce a hiccupe
                Resources.UnloadUnusedAssets();
            }
        }

    }
}
