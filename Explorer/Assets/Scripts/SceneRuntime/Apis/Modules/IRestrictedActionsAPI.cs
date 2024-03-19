using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules
{
    public interface IRestrictedActionsAPI : IDisposable
    {
        bool OpenExternalUrl(string url);
        void MovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget);
        void TeleportTo(Vector2Int newCoords);
        bool ChangeRealm(string message, string realm);
        void TriggerEmote(string predefinedEmote);
        bool TriggerSceneEmote(string src, bool loop);
        bool OpenNftDialog(string urn);
    }
}
