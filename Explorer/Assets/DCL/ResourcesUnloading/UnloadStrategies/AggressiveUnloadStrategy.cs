using System;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        private readonly int forceUnloadDuringThisFrames = 100;
        public bool isRunning { get; set; }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            if (isRunning)
                return;

            StartAggressiveUnload(cacheCleaner).Forget();
            cacheCleaner.UnloadCache();
        }

        private async UniTaskVoid StartAggressiveUnload(ICacheCleaner cacheCleaner)
        {
            Resources.UnloadUnusedAssets();
            var currentFrameRunning = 0;
            while (currentFrameRunning < forceUnloadDuringThisFrames)
            {
                cacheCleaner.UnloadCache();
                cacheCleaner.UpdateProfilingCounters();
                currentFrameRunning++;
                await UniTask.Yield();
            }

            isRunning = false;
        }

    }
}