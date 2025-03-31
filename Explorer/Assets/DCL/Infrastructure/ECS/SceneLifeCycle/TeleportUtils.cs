using Arch.Core;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public static class TeleportUtils
    {
        public static PlayerTeleportingState GetTeleportParcel(World world, Entity playerEntity)
        {
            var teleportParcel = new PlayerTeleportingState();

            if (world.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = playerTeleportIntent.Parcel;
            }

            if (world.TryGet(playerEntity, out PlayerTeleportIntent.JustTeleported justTeleported))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = justTeleported.Parcel;
            }

            return teleportParcel;
        }

        public struct PlayerTeleportingState
        {
            public Vector2Int Parcel;
            public bool IsTeleporting;
        }
    }
}
