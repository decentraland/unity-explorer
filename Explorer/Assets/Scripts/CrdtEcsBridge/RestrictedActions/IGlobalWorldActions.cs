﻿using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public interface IGlobalWorldActions
    {
        void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget);
        void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition);
        UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, CancellationToken ct);
        void TriggerEmote(URN urn, bool isLooping = false);
        bool LocalSceneDevelopment { get; }
    }
}
