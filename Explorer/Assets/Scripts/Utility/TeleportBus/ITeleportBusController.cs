using UnityEngine;

namespace Utility.TeleportBus
{
    public interface ITeleportBusController
    {
        void PushTeleportOperation(Vector2Int destinationCoordinates);
        void SubscribeToTeleportOperation(TeleportBusController.TeleportOperationDelegate callback);
    }
}
