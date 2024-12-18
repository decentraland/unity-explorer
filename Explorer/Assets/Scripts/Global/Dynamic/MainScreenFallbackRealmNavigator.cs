using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace Global.Dynamic
{
    public class MainScreenFallbackRealmNavigator : IRealmNavigator
    {
        private readonly IRealmNavigator origin;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly Entity playerEntity;
        private readonly World world;

        public MainScreenFallbackRealmNavigator(IRealmNavigator origin, IUserInAppInitializationFlow userInAppInitializationFlow, Entity playerEntity, World world)
        {
            this.origin = origin;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.playerEntity = playerEntity;
            this.world = world;
        }

        public async UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(URLDomain realm, CancellationToken ct, Vector2Int parcelToTeleport = default)
        {
            var result = await origin.TryChangeRealmAsync(realm, ct, parcelToTeleport);

            if (result.Success == false)
                DispatchFallbackToMainScreen(result.As(ChangeRealmErrors.AsTaskError), ct);

            return result;
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal)
        {
            var result = await origin.TeleportToParcelAsync(parcel, ct, isLocal);

            if (result.Success == false)
                DispatchFallbackToMainScreen(result, ct);

            return result;
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport, CancellationToken ct, Vector2Int parcelToTeleport)
        {
            try { await origin.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcelToTeleport); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.DEBUG);
                DispatchFallbackToMainScreen(EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException), ct);
            }
        }

        public void RemoveCameraSamplingData()
        {
            origin.RemoveCameraSamplingData();
        }

        private void DispatchFallbackToMainScreen(EnumResult<TaskError> recoveryError, CancellationToken ct)
        {
            ReportHub.LogError(ReportCategory.DEBUG, "Error during loading. Fallback to main screen");

            var parameters = new UserInAppInitializationFlowParameters(
                true,
                true,
                IUserInAppInitializationFlow.LoadSource.Recover,
                world,
                playerEntity,
                recoveryError
            );

            userInAppInitializationFlow.ExecuteAsync(parameters, ct).Forget();
        }
    }
}
