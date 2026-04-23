using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public interface IMovementMessageBus
    {
        public void BroadcastTeleport(string realmName, Vector3 worldPosition);

        public void Send(NetworkMovementMessage message);
    }
}
