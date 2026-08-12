using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public interface IMovementMessageBus
    {
        public void BroadcastTeleport(Vector3 worldPosition);

        public void Send(NetworkMovementMessage message);

        /// <summary>
        ///     Release any per-peer state held for <paramref name="walletId" /> once that wallet has left every room.
        ///     Keeps per-peer decode collections bounded to live peers (no unbounded growth over a session).
        /// </summary>
        public void EvictPeer(string walletId);
    }
}
