using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    /// <summary>
    /// No-op implementation of IRealmNavigator for WebGL where realm teleportation/navigation is not used.
    /// </summary>
    public class NoOpRealmNavigator : IRealmNavigator
    {
        public event Action<Vector2Int>? NavigationExecuted;

        public UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(
            URLDomain realm,
            CancellationToken ct,
            Vector2Int parcelToTeleport = default,
            bool isWorld = false) =>
            UniTask.FromResult(EnumResult<ChangeRealmError>.ErrorResult(ChangeRealmError.MessageError));

        public UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal) =>
            UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "Teleport is not available on WebGL."));

        public void RemoveCameraSamplingData() { }
    }
}
