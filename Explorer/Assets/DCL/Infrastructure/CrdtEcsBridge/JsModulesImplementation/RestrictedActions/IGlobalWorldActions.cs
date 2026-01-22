using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        UniTask<bool> MoveAndRotatePlayerAsync(Vector3 newPlayerPosition, Vector3? newCameraTarget, Vector3? newAvatarTarget, float duration, CancellationToken ct);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);
        UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, CancellationToken ct);
        UniTask TriggerEmoteAsync(URN urn, bool isLooping = false);
    }
}
