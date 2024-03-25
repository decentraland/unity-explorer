using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules
{
    public interface IRestrictedActionsAPI : IDisposable
    {
        bool TryOpenExternalUrl(string url);
        void TryMovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget);
        void TryTeleportTo(Vector2Int newCoords);
        bool TryChangeRealm(string message, string realm);
        void TryTriggerEmote(string predefinedEmote);
        bool TryTriggerSceneEmote(string src, bool loop);
        bool TryOpenNftDialog(string urn);
    }
}
