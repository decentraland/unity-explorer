using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using System;
using System.Threading;
using UnityEngine;

namespace SceneRunner.Scene
{
    public interface ISceneFacade : IUniTaskAsyncDisposable, IDisposable
    {
        SceneShortInfo Info { get; }
        ISceneStateProvider SceneStateProvider { get; }
        SceneEcsExecutor EcsExecutor { get; }
        PersistentEntities PersistentEntities { get; }
        ISceneData SceneData { get; }
        bool IsEmpty { get; }

        void Initialize();

        /// <summary>
        ///     Applies static CRDT (e.g. main.crdt) to the ECS if present. Call after Initialize and before SetRunning/StartScene.
        /// </summary>
        void ApplyStaticMessagesIfAny();

        /// <summary>
        ///     Start an update loop with a given FPS
        /// </summary>
        UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct);

        /// <summary>
        ///     Change the target fps while the scene is running.
        ///     It will take effect right after the next update
        /// </summary>
        /// <param name="fps">Target FPS</param>
        void SetTargetFPS(int fps);

        void SetIsCurrent(bool isCurrent);

        public UniTask StartScene();

        public UniTask Tick(float dt);

        /// <summary>
        ///     Opens the ECS gate so throttled groups (including <c>ComponentInstantiationGroup</c>)
        ///     run this frame. Required on WebGL where <c>crdtSendToRenderer</c> may fire
        ///     asynchronously after <see cref="Tick"/> returns, opening the gate one frame too late.
        /// </summary>
        void OpenEcsGate();

        bool Contains(Vector2Int parcel);

        bool IsSceneReady();
    }
}
