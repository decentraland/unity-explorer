using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using System;
using System.Threading;
using UnityEngine;
using RichTypes;

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
        ///     Initializes the scene and runs the JS startup code. Returns when JS onStart has completed.
        ///     Must be called from a non-main thread.
        /// </summary>
        UniTask<SceneState> StartAsync(int targetFPS, CancellationToken ct);

        /// <summary>
        ///     Runs the per-tick update loop. Should be called after <see cref="StartAsync"/> completes
        ///     and only if the scene state is still Running.
        /// </summary>
        UniTask UpdateLoopAsync(CancellationToken ct);

        /// <summary>
        ///     Change the target fps while the scene is running.
        ///     It will take effect right after the next update
        /// </summary>
        /// <param name="fps">Target FPS</param>
        void SetTargetFPS(int fps);

        void SetIsCurrent(bool isCurrent);

        internal UniTask StartScene();

        internal UniTask Tick(float dt);

        bool Contains(Vector2Int parcel);

        bool IsSceneReady();
    }
}
