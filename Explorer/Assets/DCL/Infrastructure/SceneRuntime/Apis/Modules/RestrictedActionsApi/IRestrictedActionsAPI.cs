using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.RestrictedActionsApi
{
    public interface IRestrictedActionsAPI
    {
        bool TryOpenExternalUrl(string url);

        UniTask<bool> TryMovePlayerToAsync(Vector3 newRelativePosition, Vector3? cameraTarget, Vector3? avatarTarget, float duration, CancellationToken ct);

        void TryTeleportTo(Vector2Int newCoords);

        bool TryChangeRealm(string message, string realm);

        void TryTriggerEmote(string predefinedEmote, AvatarEmoteMask mask);

        UniTask<bool> TryTriggerSceneEmoteAsync(string src, bool loop, AvatarEmoteMask mask, CancellationToken ct);

        bool TryOpenNftDialog(string urn);

        void TryCopyToClipboard(string text);

        void TryStopEmote();
    }
}
