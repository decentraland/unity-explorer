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
        void TriggerEmote(URN urn, bool isLooping = false);
        bool LocalSceneDevelopment { get; }
        UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, CancellationToken ct);
    }
}
