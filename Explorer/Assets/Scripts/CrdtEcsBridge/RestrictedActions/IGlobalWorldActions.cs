using CommunicationData.URLHelpers;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);

        // void TriggerEmote(URN urn);
    }
}
