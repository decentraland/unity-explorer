using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Seam for realm-change teleport operations to gate Pulse ingress without coupling to the concrete bus.
    /// </summary>
    public interface IPulseIngressBlocker
    {
        /// <summary>
        ///     Drops ingress and purges known peers until the realm change's outcome is announced.
        /// </summary>
        void BlockIngressUntilTeleportBroadcast();

        /// <summary>
        ///     Reverts <see cref="BlockIngressUntilTeleportBroadcast" /> after a failed realm change.
        /// </summary>
        void ResumeAfterFailedRealmChange(Vector3 currentPosition);
    }
}
