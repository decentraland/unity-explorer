using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utility.Types;
using DCL.Utility.Exceptions;
using Microsoft.ClearScript;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInSameRealmTeleportOperation : ITeleportOperation
    {
        private readonly ITeleportController teleportController;

        public MoveToParcelInSameRealmTeleportOperation(ITeleportController teleportController)
        {
            this.teleportController = teleportController;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams args, CancellationToken ct)
        {
            float finalizationProgress = args.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = args.Report.CreateChildReport(finalizationProgress);
            EnumResult<TaskError> result = await teleportController.TryTeleportToSceneSpawnPointAsync(args.CurrentDestinationParcel, teleportLoadReport, ct);
            args.Report.SetProgress(finalizationProgress);

            // See https://github.com/decentraland/unity-explorer/issues/4470: we should teleport the player even if the scene has javascript errors
            // See https://github.com/decentraland/unity-explorer/issues/6124 we should teleport the player even if the scene cannot be loaded due to a missing manifest
            // We need to prevent the error propagation, otherwise the load state remains invalid which provokes issues like the incapability of typing another command in the chat
            if (result.Error is { Exception: ScriptEngineException } or { Exception: ManifestNotFoundException })
            {
                ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Error on teleport to parcel in same realm {args.CurrentDestinationRealm}, {args.CurrentDestinationParcel}: {result.Error.Value.Exception}");
                return EnumResult<TaskError>.SuccessResult();
            }

            return result;
        }
    }
}
