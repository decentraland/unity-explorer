using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using DCL.FeatureFlags;
using Global.AppArgs;
using Global.Dynamic;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class TeleportStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;
        private Vector2Int startParcel;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IAppArgs appArgs;


        public TeleportStartupOperation(ILoadingStatus loadingStatus, IRealmNavigator realmNavigator,
            Vector2Int startParcel,
            FeatureFlagsCache featureFlagsCache, IAppArgs appArgs)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
            this.startParcel = startParcel;
            this.featureFlagsCache = featureFlagsCache;
            this.appArgs = appArgs;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = report.CreateChildReport(finalizationProgress);

            CheckParcelOverride();
            
            await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, startParcel);
            report.SetProgress(finalizationProgress);
            return Result.SuccessResult();
        }

        private void CheckParcelOverride()
        {
            string? parcelToTeleportOverride = null;

            //First we need to check if the user has passed a position as an argument. This is the case used on local scene development
            //Check https://github.com/decentraland/js-sdk-toolchain/blob/2c002ca9e6feb98a771337190db2945e013d7b93/packages/%40dcl/sdk-commands/src/commands/start/explorer-alpha.ts#L29
            if (appArgs.TryGetValue(AppArgsFlags.POSITION, out parcelToTeleportOverride))
            {
                ParsePositionAppParameter(parcelToTeleportOverride);
                return;
            }

            //If not, we check the feature flag usage
            var featureFlagOverride =
                featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.GENESIS_STARTING_PARCEL) &&
                featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.GENESIS_STARTING_PARCEL,
                    FeatureFlagsStrings.STRING_VARIANT, out parcelToTeleportOverride) &&
                parcelToTeleportOverride != null;

            if (featureFlagOverride)
                ParsePositionAppParameter(parcelToTeleportOverride);
        }

        private void ParsePositionAppParameter(string targetPositionParam)
        {
            if (!RealmHelper.TryParseParcelFromString(targetPositionParam, out var targetPosition)) return;
            startParcel = targetPosition;
        }
    }
}
