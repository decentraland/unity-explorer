using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;
using CommunicationData.URLHelpers;
using System.Threading.Tasks;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);
        UniTask TriggerSceneEmoteAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct);
        void TriggerEmote(URN urn, bool isLooping = false);

        bool LocalSceneDevelopment { get; }

        Task TriggerLocalSceneEmoteAsync(string sceneId, string name, string hash, bool loop, CancellationToken ct);
    }
}
