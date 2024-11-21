using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.FeatureFlags;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SetOverrideStartingParcelOperation : IStartupOperation
    {
        private readonly IRealmNavigator realmNavigator;
        private readonly Vector2Int startParcel;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly bool isLocalDevelopment;

        public SetOverrideStartingParcelOperation(
            IRealmNavigator realmNavigator,
            Vector2Int startParcel,
            FeatureFlagsCache featureFlagsCache,
            bool isLocalDevelopment)
        {
            this.realmNavigator = realmNavigator;
            this.startParcel = startParcel;
            this.featureFlagsCache = featureFlagsCache;
            this.isLocalDevelopment = isLocalDevelopment;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            string? parcelToTeleportOverride = null;

            bool overrideStartingParcel = !isLocalDevelopment &&
                                          startParcel == Vector2Int.zero &&
                                          featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.GENESIS_STARTING_PARCEL) &&
                                          featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.GENESIS_STARTING_PARCEL, FeatureFlagsStrings.STRING_VARIANT, out parcelToTeleportOverride) &&
                                          parcelToTeleportOverride != null;

            realmNavigator.SetOverrideValues(overrideStartingParcel, parcelToTeleportOverride!);

            await Task.CompletedTask;
            return Result.SuccessResult();
        }
    }
}
