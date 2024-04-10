using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public class ChangeRoomsRealmNavigator : IRealmNavigator
    {
        private readonly IRealmNavigator origin;
        private readonly Action reconnect;

        public ChangeRoomsRealmNavigator(IRealmNavigator origin, Action reconnect)
        {
            this.origin = origin;
            this.reconnect = reconnect;
        }

        public async UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct)
        {
            bool result = await origin.TryChangeRealmAsync(realm, ct);

            if (result)
                reconnect();

            return result;
        }

        public UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct) =>
            origin.TeleportToParcelAsync(parcel, ct);
    }
}
