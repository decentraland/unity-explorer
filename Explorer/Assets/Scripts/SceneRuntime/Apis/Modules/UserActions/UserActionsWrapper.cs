using DCL.Diagnostics;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.UserActions
{
    public class UserActionsWrapper : IJsApiWrapper
    {
        private readonly IRestrictedActionsAPI restrictedActionsAPI;
        private readonly Action<string> logWarning;

        public UserActionsWrapper(IRestrictedActionsAPI restrictedActionsAPI) : this(
            restrictedActionsAPI,
            m => ReportHub.LogWarning(ReportCategory.ENGINE, m)
        ) { }

        public UserActionsWrapper(IRestrictedActionsAPI restrictedActionsAPI, Action<string> logWarning)
        {
            this.restrictedActionsAPI = restrictedActionsAPI;
            this.logWarning = logWarning;
        }

        public void Dispose() { }

        [UsedImplicitly]
        public void RequestTeleport(string destination)
        {
            if (destination is "magic" or "crowd")
            {
                logWarning($"Destination to {destination} is outdated");
                return;
            }

            try
            {
                var coordinates = new ParcelCoordinates(destination);
                restrictedActionsAPI.TryTeleportTo(coordinates.AsVector2Int());
            }
            catch (Exception e) { logWarning($"Error while trying to teleport to {destination}: {e.Message} {e}"); }
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
