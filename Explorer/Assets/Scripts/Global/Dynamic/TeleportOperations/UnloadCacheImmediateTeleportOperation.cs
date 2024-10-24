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

        public UnloadCacheImmediateTeleportOperation(TeleportCounter teleportCounter, ICacheCleaner cacheCleaner)
        {
            this.teleportCounter = teleportCounter;
            this.cacheCleaner = cacheCleaner;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.UnloadCacheChecking);
            if (teleportCounter.ReachedTeleportLimit())
            {
                cacheCleaner.UnloadCacheImmediate();
                Resources.UnloadUnusedAssets();
            }
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}