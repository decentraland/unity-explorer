using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        UniTask<bool> MoveAndRotatePlayerAsync(Vector3 newPlayerPosition, Vector3? newCameraTarget, Vector3? newAvatarTarget, float duration, CancellationToken ct);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);
        UniTask<(URN Urn, bool IsLooping)?> TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, AvatarEmoteMask mask, CancellationToken ct);
        void TriggerEmote(URN urn, bool isLooping, AvatarEmoteMask mask);
        void StopEmote();

        /// <summary>
        /// True when masked emotes cannot be loaded/played in the current run mode and must fall back
        /// to full body. This is the case for any run mode that routes through the legacy local-load
        /// path (raw GLBs imported as legacy AnimationClip), where Mecanim runtime imports fail
        /// because GLTFast's path uses AnimationClip.SetCurve which is editor-only.
        /// Mirrors the loadFromLocalScene condition in TriggerSceneEmoteAsync.
        /// </summary>
        bool ShouldFallbackMaskedEmotesToFullBody(ISceneData sceneData);
    }
}
