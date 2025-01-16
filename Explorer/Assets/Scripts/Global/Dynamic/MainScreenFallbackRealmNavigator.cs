using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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

        public event Action<Vector2Int>? NavigationExecuted
        {
            add => origin.NavigationExecuted += value;
            remove => origin.NavigationExecuted -= value;
        }

        public MainScreenFallbackRealmNavigator(IRealmNavigator origin, IUserInAppInitializationFlow userInAppInitializationFlow, Entity playerEntity, World world)
        {
            this.origin = origin;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.playerEntity = playerEntity;
            this.world = world;
        }

        public async UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(URLDomain realm, CancellationToken ct, Vector2Int parcelToTeleport = default)
        {
            EnumResult<ChangeRealmError> result = await origin.TryChangeRealmAsync(realm, ct, parcelToTeleport);

            if (result.Success == false && !result.Error!.Value.State.IsRecoverable())
                DispatchFallbackToMainScreen(result.As(ChangeRealmErrors.AsTaskError), ct);

            return result;
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal)
        {
            EnumResult<TaskError> result = await origin.TeleportToParcelAsync(parcel, ct, isLocal);

            if (result.Success == false)
                DispatchFallbackToMainScreen(result, ct);

            return result;
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
