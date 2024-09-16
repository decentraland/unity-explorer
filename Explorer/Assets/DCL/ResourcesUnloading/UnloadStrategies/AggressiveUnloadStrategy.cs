using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        private const int FORCE_UNLOAD_DURING_THIS_FRAMES = 200;
        public bool isRunning { get; set; }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            if (isRunning)
                return;

            isRunning = true;
            Resources.UnloadUnusedAssets();
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
            }

        }

    }
}