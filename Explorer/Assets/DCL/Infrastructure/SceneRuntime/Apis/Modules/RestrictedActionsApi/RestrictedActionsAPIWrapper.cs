using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using JetBrains.Annotations;
using SceneRuntime;
using SceneRuntime.Apis;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneRuntime.Apis.RestrictedActionsApi
{
    public class RestrictedActionsAPIWrapper : JsApiWrapper
    {
        private readonly IRestrictedActionsAPI api;

        private CancellationTokenSource? triggerEmoteCancellationToken;
        private CancellationTokenSource? triggerSceneEmoteCancellationToken;

        public RestrictedActionsAPIWrapper(IRestrictedActionsAPI api, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.api = api;
        }

        [UsedImplicitly]
        public bool OpenExternalUrl(string url) =>
            api.TryOpenExternalUrl(url);

        private CancellationTokenSource? movePlayerToCancellationToken;

        [UsedImplicitly]
        public object MovePlayerTo(
            double newRelativePositionX, double newRelativePositionY, double newRelativePositionZ,
            bool hasCameraTarget, double cameraTargetX, double cameraTargetY, double cameraTargetZ,
            bool hasAvatarTarget, double avatarTargetX, double avatarTargetY, double avatarTargetZ,
            double? duration)
        {
            movePlayerToCancellationToken = movePlayerToCancellationToken.SafeRestart();
            return MovePlayerToAsync(movePlayerToCancellationToken.Token).ToDisconnectedPromise(this);

            async UniTask<bool> MovePlayerToAsync(CancellationToken ct) =>
                await api.TryMovePlayerToAsync(
                    new Vector3((float)newRelativePositionX, (float)newRelativePositionY, (float)newRelativePositionZ),
                    hasCameraTarget ? new Vector3((float)cameraTargetX, (float)cameraTargetY, (float)cameraTargetZ) : null,
                    hasAvatarTarget ? new Vector3((float)avatarTargetX, (float)avatarTargetY, (float)avatarTargetZ) : null,
                    duration.HasValue ? (float)duration.Value : 0f,
                    ct);
        }

        [UsedImplicitly]
        public void TeleportTo(bool hasCoordinates, int x, int y, string? realm)
        {
            if (!TeleportDestination.TryCreate(hasCoordinates ? new Vector2Int(x, y) : null, realm, out TeleportDestination destination))
            {
                ReportHub.LogWarning(ReportCategory.RESTRICTED_ACTIONS, "TeleportTo: request carries neither worldCoordinates nor realm");
                return;
            }

            api.TryTeleportTo(destination);
        }

        [UsedImplicitly]
        public bool ChangeRealm(string message, string realm) =>
            api.TryChangeRealm(message, realm);

        [UsedImplicitly]
        public object TriggerEmote(string predefinedEmote, uint? mask)
        {
            triggerEmoteCancellationToken = triggerEmoteCancellationToken.SafeRestart();
            return TriggerEmoteAsync(triggerEmoteCancellationToken.Token).ToDisconnectedPromise(this);

            async UniTask TriggerEmoteAsync(CancellationToken ct)
            {
                await UniTask.SwitchToMainThread();
                if (ct.IsCancellationRequested) return;
                api.TryTriggerEmote(predefinedEmote, ToAvatarEmoteMask(mask));
            }
        }

        [UsedImplicitly]
        public object TriggerSceneEmote(string src, bool loop, uint? mask)
        {
            triggerSceneEmoteCancellationToken = triggerSceneEmoteCancellationToken.SafeRestart();
            return TriggerSceneEmoteAsync(triggerSceneEmoteCancellationToken.Token).ToDisconnectedPromise(this);

            async UniTask<bool> TriggerSceneEmoteAsync(CancellationToken ct) =>
                await api.TryTriggerSceneEmoteAsync(src, loop, ToAvatarEmoteMask(mask), ct);
        }

        /// <summary>
        ///     Converts the request's optional <see cref="DCL.ECSComponents.AvatarMask" /> wire value into the runtime
        ///     <see cref="AvatarEmoteMask" />: an absent mask means full-body, <c>AM_UPPER_BODY</c> means upper-body.
        /// </summary>
        private static AvatarEmoteMask ToAvatarEmoteMask(uint? mask) =>
            mask == (uint)ECSComponents.AvatarMask.AmUpperBody
                ? AvatarEmoteMask.AemUpperBody
                : AvatarEmoteMask.AemFullBody;

        [UsedImplicitly]
        public void CopyToClipboard(string text) =>
            api.TryCopyToClipboard(text);

        [UsedImplicitly]
        public bool OpenNftDialog(string urn) =>
            api.TryOpenNftDialog(urn);

        [UsedImplicitly]
        public object OpenExplorerUi(int ui, uint requestId)
        {
            // No SafeRestart token here, unlike the siblings: it would cancel the first of two overlapping
            // calls, which is the call that owes the second one a WasAlreadyOpen answer.
            return OpenExplorerUiAsync(disposeCts.Token).ToDisconnectedPromise(this);

            async UniTask<int> OpenExplorerUiAsync(CancellationToken ct) =>
                (int)await api.TryOpenExplorerUiAsync((ExplorerUi)ui, requestId, ct);
        }

        [UsedImplicitly]
        public object StopEmote()
        {
            // Cancel any in-flight trigger operations so they don't fire after the stop
            triggerEmoteCancellationToken = triggerEmoteCancellationToken.SafeRestart();
            triggerSceneEmoteCancellationToken = triggerSceneEmoteCancellationToken.SafeRestart();

            return StopEmoteAsync().ToDisconnectedPromise(this);

            async UniTask<bool> StopEmoteAsync()
            {
                await UniTask.SwitchToMainThread();
                return api.TryStopEmote();
            }
        }

        public override void Dispose() =>
            api.Dispose();
    }
}
