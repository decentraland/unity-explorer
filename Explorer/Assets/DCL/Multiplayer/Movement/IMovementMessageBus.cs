using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public interface IMovementMessageBus
    {
        void BroadcastTeleport(Vector3 worldPosition);

        public void Send(NetworkMovementMessage message);
    }
}
