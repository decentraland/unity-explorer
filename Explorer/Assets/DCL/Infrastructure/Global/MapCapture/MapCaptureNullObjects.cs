using CommunicationData.URLHelpers;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Poses;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Global.MapCapture
{
    /// <summary>No player to move and no avatar to emote: a scene's restricted actions do nothing.</summary>
    internal class MapCaptureWorldActions : IGlobalWorldActions
    {
        public UniTask<bool> MoveAndRotatePlayerAsync(Vector3 newPlayerPosition, Vector3? newCameraTarget, Vector3? newAvatarTarget, float duration, CancellationToken ct) =>
            UniTask.FromResult(false);

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition) { }

        public UniTask<(URN Urn, bool IsLooping)?> TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, AvatarEmoteMask mask, CancellationToken ct) =>
            UniTask.FromResult<(URN Urn, bool IsLooping)?>(null);

        public void TriggerEmote(URN urn, bool isLooping, AvatarEmoteMask mask) { }

        public void StopEmote() { }
    }

    /// <summary>No comms room, so no participants.</summary>
    internal class MapCaptureRemoteMetadata : IRemoteMetadata
    {
        public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata { get; } = new Dictionary<string, IRemoteMetadata.ParticipantMetadata>();

        public void BroadcastSelfParcel(Vector2Int pose) { }

        public void BroadcastSelfMetadata() { }
    }

    /// <summary>The MVC manager the scene APIs are given has no UI; its popup closer is a button nothing draws.</summary>
    internal class MapCapturePopupCloserView : IPopupCloserView
    {
        private const string BUTTON_NAME = "MapCapturePopupCloser";

        public Button CloseButton { get; } = new GameObject(BUTTON_NAME).AddComponent<Button>();

        public void SetDrawOrder(CanvasOrdering order) { }

        public UniTask ShowAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public UniTask HideAsync(CancellationToken ct, bool isInstant = false) =>
            UniTask.CompletedTask;

        public void SetCanvasActive(bool isActive) { }
    }
}
