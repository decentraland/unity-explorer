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
        UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, AvatarEmoteMask mask, CancellationToken ct);
        void TriggerEmote(URN urn, bool isLooping, AvatarEmoteMask mask);
        void StopEmote();
    }
}
