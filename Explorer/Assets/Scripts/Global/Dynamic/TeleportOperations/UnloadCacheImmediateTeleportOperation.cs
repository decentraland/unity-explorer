using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.UserInAppInitializationFlow;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
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

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.UnloadCacheChecking);
            //Only unload if the memory usage is normal. If its different, the regular `ReleaseMemorySystem` will take care of it.
            //if (memoryUsageProvider.GetMemoryUsageStatus() == MemoryUsageStatus.NORMAL)
            //    cacheCleaner.UnloadCache(false);
            Resources.UnloadUnusedAssets();
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}