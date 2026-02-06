using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PrivateWorlds;
using DCL.RealmNavigation.LoadingOperation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities;
using DCL.Utility;
using DCL.Utility.Types;
using Utility;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    public class RealmNavigator : IRealmNavigator
    {
        private const int MAX_REALM_CHANGE_RETRIES = 3;
        private const string TELEPORT_NOT_ALLOWED_LOCAL_SCENE =
            "Teleport is not allowed in local scene development mode";

        public event Action<Vector2Int>? NavigationExecuted;

        private readonly ILoadingScreen loadingScreen;
        private readonly IRealmController realmController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;

        private Vector2Int currentParcel;

        private readonly SequentialLoadingOperation<TeleportParams> realmChangeOperations;
        private readonly SequentialLoadingOperation<TeleportParams> teleportInSameRealmOperation;
        private readonly ILoadingStatus loadingStatus;
        private readonly IAnalyticsController analyticsController;
        private readonly ILandscape landscape;
        private readonly ObjectProxy<IEventBus> eventBusProxy;

        public RealmNavigator(
            ILoadingScreen loadingScreen,
            IRealmController realmController,
            IDecentralandUrlsSource decentralandUrlsSource,
            World globalWorld,
            ObjectProxy<Entity> cameraEntity,
            CameraSamplingData cameraSamplingData,
            ILoadingStatus loadingStatus,
            ILandscape landscape,
            IAnalyticsController analyticsController,
            SequentialLoadingOperation<TeleportParams> realmChangeOperations,
            SequentialLoadingOperation<TeleportParams> teleportInSameRealmOperation,
            ObjectProxy<IEventBus> eventBusProxy)
        {
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.cameraEntity = cameraEntity;
            this.cameraSamplingData = cameraSamplingData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.globalWorld = globalWorld;
            this.loadingStatus = loadingStatus;
            this.analyticsController = analyticsController;
            this.realmChangeOperations = realmChangeOperations;
            this.teleportInSameRealmOperation = teleportInSameRealmOperation;
            this.landscape = landscape;
            this.eventBusProxy = eventBusProxy;
        }

        private bool CheckIsNewRealm(URLDomain realm)
        {
            if (!realmController.RealmData.Configured)
                return true;

            if (realm == realmController.CurrentDomain || realm == realmController.RealmData.Ipfs.CatalystBaseUrl)
                return false;

            return true;
        }

        public async UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(
            URLDomain realm,
            CancellationToken ct,
            Vector2Int parcelToTeleport = default,
            bool isWorld = false
        )
        {
            if (ct.IsCancellationRequested)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.ChangeCancelled);

            if (realmController.RealmData.IsLocalSceneDevelopment)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.LocalSceneDevelopmentBlocked);

            if (CheckIsNewRealm(realm) == false)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.SameRealm);

            if (await realmController.IsReachableAsync(realm, ct) == false)
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.NotReachable);

            // Pre-check private world permissions before loading
            if (isWorld && eventBusProxy.Configured)
            {
                ReportHub.Log(ReportCategory.REALM, $"[RealmNavigator] World change requested (isWorld=true). Checking access for realm: {realm}");
                var result = await PublishWorldAccessCheckAsync(realm, ct);
                ReportHub.Log(ReportCategory.REALM, $"[RealmNavigator] World access check result: {result}");
                if (result != WorldAccessResult.Allowed)
                    return MapToChangeRealmError(result);
            }
            else if (isWorld && !await realmController.IsUserAuthorisedToAccessWorldAsync(realm, ct))
                return EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.UnauthorizedWorldAccess);

            ReportHub.Log(ReportCategory.REALM, $"[RealmNavigator] Proceeding to change realm: {realm}");
            var operation = DoChangeRealmAsync(realm, realmController.CurrentDomain, parcelToTeleport);
            var loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(operation, ct);

            if (!loadResult.Success)
            {
                if (!globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                    globalWorld.Add(cameraEntity.Object, cameraSamplingData);

                ReportHub.LogError(ReportCategory.REALM,
                    $"Error trying to teleport to a realm {realm}: {loadResult.Error.Value.Message}");

                return loadResult.As(ChangeRealmErrors.AsChangeRealmError);
            }

            NavigationExecuted?.Invoke(parcelToTeleport);
            ReportHub.Log(ReportCategory.REALM, $"[RealmNavigator] Realm change completed: {realm}");

            return EnumResult<ChangeRealmError>.SuccessResult();
        }

        private async UniTask<WorldAccessResult> PublishWorldAccessCheckAsync(URLDomain realm, CancellationToken ct)
        {
            var eventBus = eventBusProxy.Object;
            if (eventBus == null)
                return WorldAccessResult.CheckFailed;

            string worldName = ExtractWorldNameFromRealm(realm);
            ReportHub.Log(ReportCategory.REALM, $"[RealmNavigator] Publishing CheckWorldAccessEvent for world: {worldName}");
            var resultSource = new UniTaskCompletionSource<WorldAccessResult>();
            var evt = new CheckWorldAccessEvent(worldName, null, resultSource);
            eventBus.Publish(evt);

            try
            {
                return await resultSource.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                resultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
                return WorldAccessResult.PasswordCancelled;
            }
        }

        private static EnumResult<ChangeRealmError> MapToChangeRealmError(WorldAccessResult result) =>
            result switch
            {
                WorldAccessResult.Denied => EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.WhitelistAccessDenied),
                WorldAccessResult.PasswordCancelled => EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.PasswordCancelled),
                WorldAccessResult.CheckFailed => EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.UnauthorizedWorldAccess),
                _ => EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.PasswordRequired)
            };

        private static string ExtractWorldNameFromRealm(URLDomain realm)
        {
            string realmString = realm.Value;
            const string WORLD_PATH = "/world/";
            int worldIndex = realmString.LastIndexOf(WORLD_PATH, StringComparison.OrdinalIgnoreCase);
            if (worldIndex >= 0)
            {
                string worldPart = realmString.Substring(worldIndex + WORLD_PATH.Length);
                int slashIndex = worldPart.IndexOf('/');
                return slashIndex >= 0 ? worldPart.Substring(0, slashIndex) : worldPart;
            }
            int lastSlash = realmString.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < realmString.Length - 1
                ? realmString.Substring(lastSlash + 1)
                : realmString;
        }

        private async UniTask<EnumResult<TaskError>> ExecuteTeleportOperationsAsync(
            TeleportParams teleportParams,
            SequentialLoadingOperation<TeleportParams> ops,
            string logOpName,
            int attemptsCount,
            CancellationToken ct
        )
        {
            ReportHub.LogProductionInfo($"Trying to teleport to {teleportParams.CurrentDestinationParcel}. Attempt #{attemptsCount}");
            EnumResult<TaskError> lastOpResult = await ops.ExecuteAsync(logOpName, attemptsCount, teleportParams, ct);

            if (lastOpResult.Success)
                teleportParams.Report.SetProgress(loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));

            if (lastOpResult.Success == false)
                analyticsController.Track(
                    AnalyticsEvents.General.LOADING_ERROR,
                    new JObject
                    {
                        ["type"] = "teleportation",
                        ["message"] = lastOpResult.AsResult().ErrorMessage,
                    }
                );
            else
                NavigationExecuted?.Invoke(teleportParams.CurrentDestinationParcel);

            return lastOpResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> DoChangeRealmAsync(URLDomain realm, URLDomain? fallbackRealm, Vector2Int parcelToTeleport)
        {
            return async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Changing Realm";
                const string FALLBACK_LOG_NAME = "Returning to Previous Realm";

                if (ct.IsCancellationRequested)
                    return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

                var teleportParams = new TeleportParams(realm, parcelToTeleport, parentLoadReport, loadingStatus);

                EnumResult<TaskError> opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, LOG_NAME, MAX_REALM_CHANGE_RETRIES, ct);

                if (opResult.Success)
                    return opResult;

                if (!fallbackRealm.HasValue)
                {
                    ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. No fallback realm is provided.");
                    return opResult;
                }

                // All retries failed, try with the previous realm and parcel
                ReportHub.LogWarning(ReportCategory.REALM, "All attempts failed. Trying with previous realm and parcel.");

                teleportParams.ChangeDestination(fallbackRealm.Value, currentParcel);

                opResult = await ExecuteTeleportOperationsAsync(teleportParams, realmChangeOperations, FALLBACK_LOG_NAME, 1, ct);

                if (!opResult.Success)
                    parentLoadReport.SetProgress(1);

                return opResult;
            };
        }

        public void RemoveCameraSamplingData()
        {
            if (globalWorld.Has<CameraSamplingData>(cameraEntity.Object))
                globalWorld.Remove<CameraSamplingData>(cameraEntity.Object);
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(
            Vector2Int parcel,
            CancellationToken ct,
            bool isLocal = false
        )
        {
            if (ct.IsCancellationRequested)
                return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

            if (!isLocal && realmController.RealmData.IsLocalSceneDevelopment)
                return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, TELEPORT_NOT_ALLOWED_LOCAL_SCENE);

            Result parcelCheckResult = landscape.IsParcelInsideTerrain(parcel, isLocal);

            if (!parcelCheckResult.Success)
                return parcelCheckResult.AsEnumResult(TaskError.MessageError);

            if (!isLocal && !realmController.RealmData.IsGenesis())
            {
                var enumResult = await TryChangeToGenesisAsync(parcel, ct);
                return enumResult.As(ChangeRealmErrors.AsTaskError);
            }

            EnumResult<TaskError> loadResult = await loadingScreen.ShowWhileExecuteTaskAsync(TeleportToParcelAsyncOperation(parcel), ct);

            if (!loadResult.Success)
                ReportHub.LogError(
                    ReportCategory.SCENE_LOADING,
                    $"Error trying to teleport to a parcel {parcel}: {loadResult.Error!.Value.Message}"
                );

            return loadResult;
        }

        private async UniTask<EnumResult<ChangeRealmError>> TryChangeToGenesisAsync(Vector2Int parcel, CancellationToken ct)
        {
            var genesisUrl = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
            var enumResult = await TryChangeRealmAsync(genesisUrl, ct, parcel);
            return enumResult;
        }

        private Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> TeleportToParcelAsyncOperation(Vector2Int parcel) =>
            async (parentLoadReport, ct) =>
            {
                const string LOG_NAME = "Teleporting to Parcel";

                if (ct.IsCancellationRequested)
                    return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

                var teleportParams = new TeleportParams(
                    currentDestinationParcel: parcel,
                    loadingStatus: loadingStatus,
                    report: parentLoadReport,
                    currentDestinationRealm: URLDomain.EMPTY
                );

                EnumResult<TaskError> result = await ExecuteTeleportOperationsAsync(teleportParams, teleportInSameRealmOperation, LOG_NAME, 1, ct);
                return result;
            };
    }
}
