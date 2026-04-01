using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public interface IPlayerTeleportBroadcast
    {
        void BroadcastTeleport(Vector3 worldPosition);
    }
}
