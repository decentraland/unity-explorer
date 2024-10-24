using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.ResourcesUnloading;
using DCL.UserInAppInitializationFlow;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class UnloadCacheImmediateTeleportOperation : ITeleportOperation
    {
        private readonly TeleportCounter teleportCounter;
        private readonly ICacheCleaner cacheCleaner;
        private readonly int teleportsBeforeUnload;

        public UnloadCacheImmediateTeleportOperation(TeleportCounter teleportCounter, ICacheCleaner cacheCleaner,
            int teleportsBeforeUnload)
        {
            this.teleportCounter = teleportCounter;
            this.cacheCleaner = cacheCleaner;
            this.teleportsBeforeUnload = teleportsBeforeUnload;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.UnloadCacheChecking);
            if (teleportCounter.teleportsDone >= teleportsBeforeUnload)
            {
                cacheCleaner.UnloadCacheImmediate();
                Resources.UnloadUnusedAssets();
                teleportCounter.teleportsDone = 0;
            }

            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}