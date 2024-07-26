using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.RestrictedActionsApi
{
    public interface IRestrictedActionsAPI
    {
        bool TryOpenExternalUrl(string url);

        void TryMovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget);

        void TryTeleportTo(Vector2Int newCoords);

        bool TryChangeRealm(string message, string realm);

        UniTask<bool> TryTriggerSceneEmoteAsync(string src, bool loop, CancellationToken ct);

        bool TryOpenNftDialog(string urn);
    }
}
