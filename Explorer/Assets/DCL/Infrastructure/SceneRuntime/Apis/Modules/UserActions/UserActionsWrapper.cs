using DCL.Diagnostics;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using System;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.UserActions
{
    public class UserActionsWrapper : JsApiWrapper
    {
        private readonly IRestrictedActionsAPI restrictedActionsAPI;

        public UserActionsWrapper(IRestrictedActionsAPI restrictedActionsAPI, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.restrictedActionsAPI = restrictedActionsAPI;
        }

        public override void Dispose() { }

        [UsedImplicitly]
        public void RequestTeleport(string destination)
        {
            if (destination is "magic" or "crowd")
            {
                ReportHub.LogWarning(ReportCategory.ENGINE, $"Destination to {destination} is outdated");
                return;
            }

            try
            {
                var coordinates = new ParcelCoordinates(destination);
                restrictedActionsAPI.TryTeleportTo(coordinates.AsVector2Int());
            }
            catch (Exception e) { ReportHub.LogWarning(ReportCategory.ENGINE, $"Error while trying to teleport to {destination}: {e.Message} {e}"); }
        }

        private readonly struct ParcelCoordinates
        {
            private readonly int x;
            private readonly int y;

            public ParcelCoordinates(string coordinates) : this(coordinates.Split(',')!) { }

            public ParcelCoordinates(string[] coordinateArray) : this(
                int.Parse(coordinateArray[0]),
                int.Parse(coordinateArray[1])
            ) { }

            public ParcelCoordinates(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public static implicit operator Vector2Int(ParcelCoordinates coords) =>
                new (coords.x, coords.y);

            public Vector2Int AsVector2Int() =>
                new (x, y);

            public override string ToString() =>
                $"{x},{y}";
        }
    }
}
