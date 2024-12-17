using System;
using UnityEngine;

namespace Utility.TeleportBus
{
    public class TeleportBusController : ITeleportBusController
    {
        public delegate void TeleportOperationDelegate(Vector2Int destinationCoordinates);

        private TeleportOperationDelegate teleportOperationDelegate;

        public void PushTeleportOperation(Vector2Int destinationCoordinates) =>
            teleportOperationDelegate?.Invoke(destinationCoordinates);

        public void SubscribeToTeleportOperation(TeleportOperationDelegate callback)=>
            teleportOperationDelegate += callback;
    }
}
