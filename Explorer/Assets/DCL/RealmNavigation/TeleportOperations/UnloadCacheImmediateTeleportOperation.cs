using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class UnloadCacheImmediateTeleportOperation : ITeleportOperation
    {
        private readonly ICacheCleaner cacheCleaner;
        private readonly IMemoryUsageProvider memoryUsageProvider;

        public UnloadCacheImmediateTeleportOperation(ICacheCleaner cacheCleaner,
            IMemoryUsageProvider memoryUsageProvider)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryUsageProvider = memoryUsageProvider;
        }

        public UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.UnloadCacheChecking);
            //Only unload if the memory usage is normal. If its different, the regular `ReleaseMemorySystem` will take care of it.
            //if (memoryUsageProvider.GetMemoryUsageStatus() == MemoryUsageStatus.NORMAL)
            //    cacheCleaner.UnloadCache(false);
            Resources.UnloadUnusedAssets();
            return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }
}
