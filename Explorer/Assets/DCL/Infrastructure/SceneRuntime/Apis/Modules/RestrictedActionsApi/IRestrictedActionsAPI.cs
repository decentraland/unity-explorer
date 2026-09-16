using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SceneRuntime.Apis.RestrictedActionsApi
{
    public interface IRestrictedActionsAPI : IDisposable
    {
        bool TryOpenExternalUrl(string url);

        UniTask<bool> TryMovePlayerToAsync(Vector3 newRelativePosition, Vector3? cameraTarget, Vector3? avatarTarget, float duration, CancellationToken ct);

        /// <summary>
        ///     Teleports the player. <paramref name="coords" /> absent targets the realm's default spawn (only
        ///     meaningful together with <paramref name="realm" />); <paramref name="realm" /> absent addresses the
        ///     parcel in the realm the player is already in. Both absent is rejected (warning logged, no-op).
        /// </summary>
        void TryTeleportTo(Vector2Int? coords, string? realm);

        bool TryChangeRealm(string message, string realm);

        void TryTriggerEmote(string predefinedEmote, AvatarEmoteMask mask);

        UniTask<bool> TryTriggerSceneEmoteAsync(string src, bool loop, AvatarEmoteMask mask, CancellationToken ct);

        bool TryOpenNftDialog(string urn);

        int TryOpenExplorerUi(int ui);

        void TryCopyToClipboard(string text);

        void TryStopEmote();
    }
}
