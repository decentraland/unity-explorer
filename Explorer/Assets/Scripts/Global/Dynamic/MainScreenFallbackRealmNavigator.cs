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
                DispatchFallbackToMainScreen(ct);

            return result;
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal)
        {
            var result = await origin.TeleportToParcelAsync(parcel, ct, isLocal);

            if (result.Success == false)
                DispatchFallbackToMainScreen(ct);

            return result;
        }

        public async UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport, CancellationToken ct, Vector2Int parcelToTeleport)
        {
            try { await origin.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcelToTeleport); }
            catch (Exception) { DispatchFallbackToMainScreen(ct); }
        }

        private void DispatchFallbackToMainScreen(CancellationToken ct)
        {
            ReportHub.LogError(ReportCategory.DEBUG, "Error during loading. Fallback to main screen");

            var parameters = new UserInAppInitializationFlowParameters(
                true,
                true,
                true,
                IUserInAppInitializationFlow.LoadSource.Recover,
                world,
                playerEntity
            );

            userInAppInitializationFlow.ExecuteAsync(parameters, ct).Forget();
        }
    }
}
