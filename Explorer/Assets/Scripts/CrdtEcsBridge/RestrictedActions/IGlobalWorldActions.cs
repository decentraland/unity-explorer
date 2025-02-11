﻿using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget, Quaternion? newRotation);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);
        UniTask TriggerSceneEmoteAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct);
        void TriggerEmote(URN urn, bool isLooping = false);
    }
}
