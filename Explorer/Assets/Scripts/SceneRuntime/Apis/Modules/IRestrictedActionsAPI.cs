using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules
{
    public interface IRestrictedActionsAPI : IDisposable
    {
        bool OpenExternalUrl(string url);
        void MovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget);
        void TeleportTo(Vector2Int newCoords);
        bool ChangeRealm(string realm);
    }
}
